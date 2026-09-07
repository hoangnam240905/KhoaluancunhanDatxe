using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Contracts;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class ContractService(CarRentalDbContext db, IRealtimePublisher? realtime = null)
{
    public async Task<(ContractResponse? Contract, bool Created, string? Error, int StatusCode)> CreateAsync(
        int customerId, int bookingId)
    {
        var booking = await db.Bookings
            .Include(b => b.Customer).ThenInclude(c => c.User)
            .Include(b => b.VehicleType)
            .Include(b => b.Contract)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);
        if (booking is null)
            return Fail("Không tìm thấy đơn.", StatusCodes.Status404NotFound);
        if (booking.CustomerId != customerId)
            return Fail("Không có quyền tạo hợp đồng cho đơn này.", StatusCodes.Status403Forbidden);
        if (booking.Status == BookingStatuses.Cancelled)
            return Fail("Không thể tạo hợp đồng cho đơn đã hủy.", StatusCodes.Status400BadRequest);

        if (booking.Contract is not null)
            return (Map(booking.Contract), false, null, StatusCodes.Status200OK);

        var contract = new Contract
        {
            BookingId = booking.BookingId,
            ContractNumber = $"CTR-{booking.BookingId:D6}",
            Status = ContractStatuses.Issued,
            CustomerId = booking.CustomerId,
            CustomerName = booking.Customer.User.FullName,
            VehicleTypeName = booking.VehicleType.TypeName,
            RentalMode = booking.RentalMode,
            PickupAddress = booking.PickupAddress,
            DropoffAddress = booking.DropoffAddress,
            StartDate = booking.StartDate,
            EndDate = booking.EndDate,
            TotalAmount = booking.TotalAmount,
            DepositAmount = booking.QuotedDepositAmount,
            CreatedAt = DateTime.UtcNow
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        await RealtimeNotify.ContractStatusChanged(
            realtime, booking.CustomerId, booking.BookingId, contract.ContractId, contract.Status);

        return (Map(contract), true, null, StatusCodes.Status201Created);
    }

    public async Task<(ContractResponse? Contract, string? Error, int StatusCode)> GetByBookingAsync(
        int userId, string? role, int bookingId)
    {
        var booking = await db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.BookingId == bookingId);
        if (booking is null)
            return (null, "Không tìm thấy đơn.", StatusCodes.Status404NotFound);

        if (role == RoleNames.Driver)
            return (null, "Không có quyền xem hợp đồng.", StatusCodes.Status403Forbidden);

        if (role == RoleNames.Customer && booking.CustomerId != userId)
            return (null, "Không có quyền xem hợp đồng đơn này.", StatusCodes.Status403Forbidden);

        if (role is not (RoleNames.Customer or RoleNames.Admin or RoleNames.Dispatcher))
            return (null, "Không có quyền xem hợp đồng.", StatusCodes.Status403Forbidden);

        var contract = await db.Contracts.AsNoTracking().FirstOrDefaultAsync(c => c.BookingId == bookingId);
        if (contract is null)
            return (null, "Chưa có hợp đồng.", StatusCodes.Status404NotFound);

        return (Map(contract), null, StatusCodes.Status200OK);
    }

    public async Task<(ContractResponse? Contract, string? Error, int StatusCode)> SimulateSignAsync(
        int customerId, int contractId)
    {
        var contract = await db.Contracts.Include(c => c.Booking)
            .FirstOrDefaultAsync(c => c.ContractId == contractId);
        if (contract is null)
            return (null, "Không tìm thấy hợp đồng.", StatusCodes.Status404NotFound);
        if (contract.CustomerId != customerId)
            return (null, "Không có quyền ký hợp đồng này.", StatusCodes.Status403Forbidden);
        if (contract.Booking.Status == BookingStatuses.Cancelled
            || contract.Status == ContractStatuses.Voided)
            return (null, "Không thể ký hợp đồng đã hủy.", StatusCodes.Status400BadRequest);

        if (contract.Status == ContractStatuses.Signed)
            return (Map(contract), null, StatusCodes.Status200OK);

        contract.Status = ContractStatuses.Signed;
        contract.SignedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await RealtimeNotify.ContractStatusChanged(
            realtime, contract.CustomerId, contract.BookingId, contract.ContractId, contract.Status);

        return (Map(contract), null, StatusCodes.Status200OK);
    }

    public async Task<List<ContractResponse>> GetAdminAsync(int? bookingId, string? status)
    {
        var query = db.Contracts.AsNoTracking().AsQueryable();
        if (bookingId is not null)
            query = query.Where(c => c.BookingId == bookingId.Value);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(c => c.Status == status.Trim());

        var rows = await query.OrderBy(c => c.ContractId).ToListAsync();
        return rows.Select(Map).ToList();
    }

    internal static ContractResponse Map(Contract c) => new(
        c.ContractId,
        c.BookingId,
        c.ContractNumber,
        c.Status,
        c.CustomerId,
        c.CustomerName,
        c.VehicleTypeName,
        c.RentalMode,
        c.PickupAddress,
        c.DropoffAddress,
        c.StartDate,
        c.EndDate,
        c.TotalAmount,
        c.DepositAmount,
        c.CreatedAt,
        c.SignedAt);

    private static (ContractResponse? Contract, bool Created, string? Error, int StatusCode) Fail(
        string error, int status) => (null, false, error, status);
}
