using Backend.Constants;
using Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend.Validation;

public static class BookingDispatchReadinessRules
{
    public const string ContractNotSigned = "Khách hàng chưa ký hợp đồng điện tử.";
    public const string DepositNotPaid = "Đơn thuê chưa thanh toán tiền cọc.";

    public static async Task<string?> GetBlockReasonAsync(CarRentalDbContext db, int bookingId)
    {
        var signed = await db.Contracts.AnyAsync(c =>
            c.BookingId == bookingId && c.Status == ContractStatuses.Signed);
        if (!signed)
            return ContractNotSigned;

        var paid = await db.Payments.AnyAsync(p =>
            p.BookingId == bookingId
            && p.PaymentType == PaymentTypes.Deposit
            && p.Status == PaymentStatuses.Paid);
        if (!paid)
            return DepositNotPaid;

        return null;
    }
}
