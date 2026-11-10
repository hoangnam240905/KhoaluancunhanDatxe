using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Bookings;
using Backend.Entities;
using Backend.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>
/// Closing the settlement of a completed trip (plan1.md "Làm quyết toán chuyến thuê chạy thật").
/// Total = final amount fixed at completion (extra km, late, fuel, vehicle-change difference already in it)
/// + fees the dispatcher types now (extra fuel, damage, other). Remaining = Total − what the customer has paid;
/// recorded as a simulated Balance (collect) or Refund payment, like the rest of the payment simulation.
/// </summary>
public class SettlementService(CarRentalDbContext db, BookingService bookings)
{
    public const string NotFound = "Không tìm thấy đơn.";
    public const string NotCompleted = "Chỉ quyết toán đơn đã hoàn thành.";
    public const string AlreadySettled = "Đơn đã quyết toán.";
    public const string FeeInvalid = "Phí nhập tay phải từ 0 đến 1.000.000.000 ₫.";
    public const string NoteTooLong = "Ghi chú tối đa 500 ký tự.";
    public const string MethodInvalid = "Phương thức thanh toán không hợp lệ.";

    private const decimal MaxFee = 1_000_000_000m;

    private static string Vnd(decimal v) => v.ToString("#,0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + " ₫";

    /// <summary>Money actually received: paid payments minus paid refunds.</summary>
    public static decimal PaidAmount(IEnumerable<Payment> payments)
    {
        decimal paid = 0;
        foreach (var p in payments)
        {
            if (p.Status != PaymentStatuses.Paid)
                continue;
            paid += p.PaymentType == PaymentTypes.Refund ? -p.Amount : p.Amount;
        }
        return paid;
    }

    public Task<(BookingResponse? Data, string? Error, bool NotFound)> SettleAsync(
        int bookingId, int dispatcherId, SettleBookingRequest? request)
        => SqliteWriteLock.ExecuteAsync(db, () => SettleCoreAsync(bookingId, dispatcherId, request ?? new(null, null, null, null, null)));

    private async Task<(BookingResponse? Data, string? Error, bool NotFound)> SettleCoreAsync(
        int bookingId, int dispatcherId, SettleBookingRequest request)
    {
        var booking = await db.Bookings
            .Include(b => b.Payments)
            .Include(b => b.Fees)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);
        if (booking is null)
            return (null, NotFound, true);
        if (booking.Status != BookingStatuses.Completed)
            return (null, NotCompleted, false);
        if (SettlementStatuses.IsSettled(booking.SettlementStatus))
            return (null, AlreadySettled, false);

        var extraFuel = request.ExtraFuel ?? 0;
        var damage = request.Damage ?? 0;
        var other = request.Other ?? 0;
        if (new[] { extraFuel, damage, other }.Any(v => v is < 0 or > MaxFee))
            return (null, FeeInvalid, false);
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is { Length: > 500 })
            return (null, NoteTooLong, false);
        var method = PaymentMethods.Cash;
        if (!string.IsNullOrWhiteSpace(request.PaymentMethod) && !PaymentMethods.TryResolve(request.PaymentMethod, out method))
            return (null, MethodInvalid, false);

        // Older completed rows may have no FinalAmount: the quote plus fees outside the base is the best known total.
        var baseTotal = booking.FinalAmount
            ?? booking.TotalAmount + booking.Fees.Where(f => !BookingFeeTypes.IsIncludedInBase(f.FeeType)).Sum(f => f.Amount);
        var now = DateTime.UtcNow;
        void AddFee(string type, decimal amount, string description)
        {
            if (amount <= 0)
                return;
            db.BookingFees.Add(new BookingFee { BookingId = bookingId, FeeType = type, Description = description, Amount = amount, CreatedAt = now });
        }
        AddFee(BookingFeeTypes.Fuel, extraFuel, "Nhiên liệu bổ sung khi quyết toán");
        AddFee(BookingFeeTypes.Damage, damage, note is null ? "Hư hỏng khi quyết toán" : $"Hư hỏng khi quyết toán — {note}");
        AddFee(BookingFeeTypes.Other, other, note is null ? "Phí khác khi quyết toán" : $"Phí khác khi quyết toán — {note}");

        var total = baseTotal + extraFuel + damage + other;
        var remaining = total - PaidAmount(booking.Payments);
        booking.FinalAmount = total;

        if (remaining != 0)
            db.Payments.Add(new Payment
            {
                BookingId = bookingId,
                PaymentType = remaining > 0 ? PaymentTypes.Balance : PaymentTypes.Refund,
                Amount = Math.Abs(remaining),
                Method = method,
                Status = PaymentStatuses.Paid,
                TransactionRef = $"SETTLE-{bookingId}-{now:yyyyMMddHHmmss}",
                PaidAt = now,
                CreatedAt = now
            });

        var actor = await ActorLookup.FindAsync(db, dispatcherId);
        booking.SettlementStatus = SettlementStatuses.Settled;
        booking.SettledAt = now;
        booking.SettledByUserId = actor?.UserId;
        booking.SettledByName = actor?.Name;
        booking.SettlementNote = note;
        booking.UpdatedAt = now;

        UserNotifications.Add(db, booking.CustomerId, $"Đơn #{bookingId} đã quyết toán",
            remaining switch
            {
                > 0 => $"Tổng chi phí chuyến: {Vnd(total)}. Đã thu thêm {Vnd(Math.Abs(remaining))}.",
                < 0 => $"Tổng chi phí chuyến: {Vnd(total)}. Đã hoàn lại {Vnd(Math.Abs(remaining))}.",
                _ => $"Tổng chi phí chuyến: {Vnd(total)}. Không phát sinh thu thêm hay hoàn lại."
            }, NotificationRules.TypeBooking);

        await db.SaveChangesAsync();
        return (await bookings.GetBookingByIdAsync(bookingId), null, false);
    }
}
