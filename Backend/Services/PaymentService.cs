using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Payments;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class PaymentService(CarRentalDbContext db)
{
    public async Task<(PaymentResponse? Payment, string? Error, int StatusCode)> CreateAsync(
        int customerId,
        CreatePaymentRequest request)
    {
        if (!PaymentTypes.TryResolve(request.PaymentType, out var paymentType))
            return Fail("Loại thanh toán không hợp lệ.", StatusCodes.Status400BadRequest);

        if (paymentType == PaymentTypes.Refund)
            return Fail("Refund chưa được hỗ trợ ở giai đoạn này.", StatusCodes.Status400BadRequest);

        if (paymentType == PaymentTypes.Balance)
            return Fail("Thanh toán phần còn lại chưa được hỗ trợ ở giai đoạn này.", StatusCodes.Status400BadRequest);

        if (!PaymentMethods.TryResolve(request.Method, out var method))
            return Fail("Phương thức thanh toán không hợp lệ.", StatusCodes.Status400BadRequest);

        if (request.TransactionRef is { Length: > 100 })
            return Fail("Mã giao dịch không hợp lệ.", StatusCodes.Status400BadRequest);

        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.BookingId == request.BookingId);
        if (booking is null)
            return Fail("Không tìm thấy đơn.", StatusCodes.Status404NotFound);

        if (booking.CustomerId != customerId)
            return Fail("Không có quyền thanh toán đơn này.", StatusCodes.Status403Forbidden);

        if (paymentType != PaymentTypes.Deposit)
            return Fail("Loại thanh toán không hợp lệ.", StatusCodes.Status400BadRequest);

        if (booking.QuotedDepositAmount is null)
            return Fail("Đơn hàng chưa có thông tin tiền cọc.", StatusCodes.Status400BadRequest);

        var hasActiveDeposit = await db.Payments.AnyAsync(p =>
            p.BookingId == booking.BookingId
            && p.PaymentType == PaymentTypes.Deposit
            && (p.Status == PaymentStatuses.Pending || p.Status == PaymentStatuses.Paid));
        if (hasActiveDeposit)
            return Fail("Đơn này đã có khoản cọc đang chờ hoặc đã thanh toán.", StatusCodes.Status400BadRequest);

        var payment = new Payment
        {
            BookingId = booking.BookingId,
            PaymentType = PaymentTypes.Deposit,
            Amount = booking.QuotedDepositAmount.Value,
            Method = method,
            Status = PaymentStatuses.Pending,
            TransactionRef = string.IsNullOrWhiteSpace(request.TransactionRef)
                ? null
                : request.TransactionRef.Trim(),
            PaidAt = null,
            CreatedAt = DateTime.UtcNow
        };

        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        return (Map(payment), null, StatusCodes.Status201Created);
    }

    public async Task<(List<PaymentResponse>? Payments, string? Error, int StatusCode)> GetByBookingAsync(
        int customerId,
        int bookingId)
    {
        var booking = await db.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);
        if (booking is null)
            return (null, "Không tìm thấy đơn.", StatusCodes.Status404NotFound);

        if (booking.CustomerId != customerId)
            return (null, "Không có quyền xem thanh toán đơn này.", StatusCodes.Status403Forbidden);

        var rows = await db.Payments
            .AsNoTracking()
            .Where(p => p.BookingId == bookingId)
            .OrderBy(p => p.PaymentId)
            .ToListAsync();

        return (rows.Select(Map).ToList(), null, StatusCodes.Status200OK);
    }

    private static PaymentResponse Map(Payment p) => new(
        p.PaymentId,
        p.BookingId,
        p.PaymentType,
        p.Amount,
        p.Method,
        p.Status,
        p.TransactionRef,
        p.PaidAt,
        p.CreatedAt);

    private static (PaymentResponse? Payment, string? Error, int StatusCode) Fail(string error, int status) =>
        (null, error, status);
}
