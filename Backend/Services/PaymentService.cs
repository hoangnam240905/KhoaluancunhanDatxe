using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Payments;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class PaymentService(CarRentalDbContext db, ScheduleConflictService schedule, IRealtimePublisher? realtime = null)
{
    public const string CancelledBookingDeposit = "Không thể tạo tiền cọc cho đơn đã hủy.";

    public Task<(PaymentResponse? Payment, string? Error, int StatusCode)> CreateAsync(
        int customerId,
        CreatePaymentRequest request)
        => SqliteWriteLock.ExecuteAsync(db, () => CreateCoreAsync(customerId, request));

    private async Task<(PaymentResponse? Payment, string? Error, int StatusCode)> CreateCoreAsync(
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

        if (booking.Status == BookingStatuses.Cancelled)
            return Fail(CancelledBookingDeposit, StatusCodes.Status400BadRequest);

        if (booking.QuotedDepositAmount is null)
            return Fail("Đơn hàng chưa có thông tin tiền cọc.", StatusCodes.Status400BadRequest);

        var hasActiveDeposit = await db.Payments.AnyAsync(p =>
            p.BookingId == booking.BookingId
            && p.PaymentType == PaymentTypes.Deposit
            && (p.Status == PaymentStatuses.Pending || p.Status == PaymentStatuses.Paid));
        if (hasActiveDeposit)
            return Fail("Đơn này đã có khoản cọc đang chờ hoặc đã thanh toán.", StatusCodes.Status400BadRequest);

        var holdError = await TryHoldVehicleAsync(booking, request.VehicleId);
        if (holdError is not null)
            return Fail(holdError, StatusCodes.Status400BadRequest);

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

        await RealtimeNotify.PaymentStatusChanged(
            realtime, booking.CustomerId, booking.BookingId, payment.PaymentId, payment.Status);
        await RealtimeNotify.BookingStatusChanged(
            realtime, booking.CustomerId, booking.BookingId, booking.Status, booking.AssignedVehicleId);

        return (Map(payment), null, StatusCodes.Status201Created);
    }

    public Task<(PaymentResponse? Payment, string? Error, int StatusCode)> SimulateSuccessAsync(
        int customerId, int paymentId)
        => SqliteWriteLock.ExecuteAsync(db, () => SimulateCoreAsync(customerId, paymentId, paid: true));

    public Task<(PaymentResponse? Payment, string? Error, int StatusCode)> SimulateFailureAsync(
        int customerId, int paymentId)
        => SqliteWriteLock.ExecuteAsync(db, () => SimulateCoreAsync(customerId, paymentId, paid: false));

    private async Task<(PaymentResponse? Payment, string? Error, int StatusCode)> SimulateCoreAsync(
        int customerId, int paymentId, bool paid)
    {
        var payment = await db.Payments.Include(p => p.Booking)
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId);
        if (payment is null)
            return Fail("Không tìm thấy thanh toán.", StatusCodes.Status404NotFound);
        if (payment.Booking.CustomerId != customerId)
            return Fail("Không có quyền thanh toán đơn này.", StatusCodes.Status403Forbidden);
        if (payment.Status != PaymentStatuses.Pending)
            return Fail("Chỉ mô phỏng thanh toán khi khoản đang chờ.", StatusCodes.Status400BadRequest);

        var booking = payment.Booking;
        if (paid)
        {
            payment.Status = PaymentStatuses.Paid;
            payment.PaidAt = DateTime.UtcNow;
            payment.TransactionRef ??= $"SIM-{payment.PaymentId}";
        }
        else
        {
            payment.Status = PaymentStatuses.Failed;
            payment.PaidAt = null;
            await ReleasePendingHoldIfNeededAsync(booking, payment.PaymentId);
        }

        await db.SaveChangesAsync();

        await RealtimeNotify.PaymentStatusChanged(
            realtime, booking.CustomerId, booking.BookingId, payment.PaymentId, payment.Status);
        await RealtimeNotify.BookingStatusChanged(
            realtime, booking.CustomerId, booking.BookingId, booking.Status, booking.AssignedVehicleId);

        return (Map(payment), null, StatusCodes.Status200OK);
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

    public async Task<(List<PaymentResponse>? Payments, string? Error, int StatusCode)> GetAdminAsync(
        int? bookingId, string? status, string? paymentType)
    {
        if (bookingId is not null)
        {
            var exists = await db.Bookings.AsNoTracking().AnyAsync(b => b.BookingId == bookingId.Value);
            if (!exists)
                return (null, "Không tìm thấy đơn.", StatusCodes.Status404NotFound);
        }

        var query = db.Payments.AsNoTracking().AsQueryable();
        if (bookingId is not null)
            query = query.Where(p => p.BookingId == bookingId.Value);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.Status == status.Trim());
        if (!string.IsNullOrWhiteSpace(paymentType))
            query = query.Where(p => p.PaymentType == paymentType.Trim());

        var rows = await query.OrderBy(p => p.PaymentId).ToListAsync();
        return (rows.Select(Map).ToList(), null, StatusCodes.Status200OK);
    }

    private async Task<string?> TryHoldVehicleAsync(Booking booking, int? requestedVehicleId)
    {
        var vehicleId = requestedVehicleId ?? booking.AssignedVehicleId;
        if (vehicleId is null or <= 0)
            return null;

        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == vehicleId.Value);
        if (vehicle is null)
            return "Không tìm thấy xe.";
        if (vehicle.Status != VehicleStatuses.Available)
            return "Xe không khả dụng.";
        if (await schedule.IsVehicleBlockedByMaintenanceDueAsync(vehicle.VehicleId))
            return MaintenanceLock.BlockedForNewSchedule;
        if (vehicle.TypeId != booking.VehicleTypeId)
            return "Xe không thuộc loại xe được đặt.";
        if (await schedule.HasVehicleConflictAsync(
            vehicle.VehicleId, booking.StartDate, booking.EndDate, booking.BookingId))
            return "Xe đã có lịch thuê khác trong khoảng thời gian này.";

        booking.AssignedVehicleId = vehicle.VehicleId;
        return null;
    }

    private async Task ReleasePendingHoldIfNeededAsync(Booking booking, int failedPaymentId)
    {
        if (booking.Status != BookingStatuses.Pending)
            return;

        var stillActive = await db.Payments.AnyAsync(p =>
            p.BookingId == booking.BookingId
            && p.PaymentId != failedPaymentId
            && p.PaymentType == PaymentTypes.Deposit
            && (p.Status == PaymentStatuses.Pending || p.Status == PaymentStatuses.Paid));
        if (stillActive)
            return;

        booking.AssignedVehicleId = null;
    }

    internal static PaymentResponse Map(Payment p) => new(
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
