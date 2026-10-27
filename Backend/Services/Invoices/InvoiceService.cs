using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Backend.Constants;
using Backend.Data;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services.Invoices;

public static class InvoiceKinds
{
    public const string DepositReceipt = "DepositReceipt";
    public const string TripInvoice = "TripInvoice";
}

/// <summary>A rendered receipt / invoice. The same HTML is emailed and shown on the web.</summary>
public sealed record InvoiceDocument(
    string Kind, string Number, string Title, DateTime IssuedAtUtc, int BookingId, int CustomerId,
    string? CustomerEmail, string Html);

/// <summary>
/// plan1.md Giai đoạn 2 (D4). The system only takes deposits, so there are two documents:
/// (a) deposit receipt when a deposit payment is Paid; (b) trip invoice when the booking is Completed
/// (total, deposit already paid, amount still due). Every value is HTML-encoded.
/// </summary>
public class InvoiceService(CarRentalDbContext db)
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    // Keep Vietnamese readable; markup characters (< > & " ') are still encoded.
    private static readonly HtmlEncoder Enc = HtmlEncoder.Create(UnicodeRanges.All);

    public static string DepositNumber(int paymentId) => $"HD-{paymentId:D6}";
    public static string TripNumber(int bookingId) => $"HDC-{bookingId:D6}";

    /// <summary>Only for a PAID deposit payment; otherwise null.</summary>
    public async Task<InvoiceDocument?> BuildDepositReceiptAsync(int paymentId, CancellationToken ct = default)
    {
        var payment = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.PaymentId == paymentId, ct);
        if (payment is null || payment.Status != PaymentStatuses.Paid
            || (payment.PaymentType is not null && payment.PaymentType != PaymentTypes.Deposit))
            return null;
        var booking = await LoadBookingAsync(payment.BookingId, ct);
        if (booking is null) return null;

        var paidAt = payment.PaidAt ?? payment.CreatedAt;
        var number = DepositNumber(payment.PaymentId);
        var rows = new List<(string, string)>
        {
            ("Số tiền cọc", Money(payment.Amount)),
            ("Phương thức", MethodLabel(payment.Method)),
            ("Mã giao dịch", payment.TransactionRef ?? "—"),
            ("Thời gian thanh toán", Vn(paidAt)),
            ("Tổng giá trị đơn (tạm tính)", Money(booking.TotalAmount)),
            ("Còn lại dự kiến", Money(Math.Max(0, booking.TotalAmount - payment.Amount)))
        };
        var html = Render("BIÊN NHẬN ĐẶT CỌC", number, paidAt, booking, rows,
            "Thanh toán trực tuyến đang ở chế độ mô phỏng. Số tiền còn lại thanh toán theo hóa đơn chuyến khi hoàn thành.");
        return new InvoiceDocument(InvoiceKinds.DepositReceipt, number, $"Biên nhận đặt cọc {number}", paidAt,
            booking.BookingId, booking.CustomerId, booking.Customer.User.Email, html);
    }

    /// <summary>Only for a COMPLETED booking; otherwise null.</summary>
    public async Task<InvoiceDocument?> BuildTripInvoiceAsync(int bookingId, CancellationToken ct = default)
    {
        var booking = await LoadBookingAsync(bookingId, ct);
        if (booking is null || booking.Status != BookingStatuses.Completed) return null;

        var total = booking.FinalAmount ?? booking.TotalAmount;
        var deposits = await db.Payments.AsNoTracking()
            .Where(p => p.BookingId == bookingId && p.Status == PaymentStatuses.Paid
                && (p.PaymentType == null || p.PaymentType == PaymentTypes.Deposit))
            .Select(p => p.Amount)
            .ToListAsync(ct);
        var paid = deposits.Sum();
        var due = total - paid;
        var fees = await db.BookingFees.AsNoTracking()
            .Where(f => f.BookingId == bookingId)
            .OrderBy(f => f.FeeId)
            .ToListAsync(ct);

        var issuedAt = booking.UpdatedAt ?? DateTime.UtcNow;
        var number = TripNumber(bookingId);
        var rows = new List<(string, string)>
        {
            ("Giá thuê (báo giá)", Money(booking.TotalAmount)),
            ("Phát sinh khi trả xe", Money(total - booking.TotalAmount))
        };
        // Breakdown of the adjustment above (not added again).
        rows.AddRange(fees.Select(f => ($"  • gồm: {f.Description ?? f.FeeType}", Money(f.Amount))));
        rows.Add(("Tổng tiền chuyến", Money(total)));
        rows.Add(("Đã đặt cọc", Money(paid)));
        rows.Add((due >= 0 ? "Còn lại phải trả" : "Hoàn lại cho khách", Money(Math.Abs(due))));

        var html = Render("HÓA ĐƠN CHUYẾN THUÊ XE", number, issuedAt, booking, rows,
            "Các dòng \"gồm\" là chi tiết của khoản phát sinh, không cộng thêm. Hệ thống chưa thu phần còn lại trực tuyến.");
        return new InvoiceDocument(InvoiceKinds.TripInvoice, number, $"Hóa đơn chuyến {number}", issuedAt,
            booking.BookingId, booking.CustomerId, booking.Customer.User.Email, html);
    }

    /// <summary>Every document available for this booking (paid deposit receipts, then the trip invoice).</summary>
    public async Task<IReadOnlyList<InvoiceDocument>> GetForBookingAsync(int bookingId, CancellationToken ct = default)
    {
        var docs = new List<InvoiceDocument>();
        var paymentIds = await db.Payments.AsNoTracking()
            .Where(p => p.BookingId == bookingId && p.Status == PaymentStatuses.Paid)
            .OrderBy(p => p.PaymentId).Select(p => p.PaymentId).ToListAsync(ct);
        foreach (var id in paymentIds)
            if (await BuildDepositReceiptAsync(id, ct) is { } receipt) docs.Add(receipt);
        if (await BuildTripInvoiceAsync(bookingId, ct) is { } invoice) docs.Add(invoice);
        return docs;
    }

    private Task<Booking?> LoadBookingAsync(int bookingId, CancellationToken ct)
        => db.Bookings.AsNoTracking()
            .Include(b => b.Customer).ThenInclude(c => c.User)
            .Include(b => b.VehicleType)
            .Include(b => b.AssignedVehicle)
            .Include(b => b.TripAssignment).ThenInclude(t => t!.Vehicle)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, ct);

    private static string Render(string heading, string number, DateTime issuedUtc, Booking b,
        IEnumerable<(string Label, string Value)> money, string note)
    {
        var vehicle = b.TripAssignment?.Vehicle ?? b.AssignedVehicle;
        var vehicleText = vehicle is null
            ? b.VehicleType.TypeName
            : $"{vehicle.Brand} {vehicle.Model} — {vehicle.LicensePlate} ({b.VehicleType.TypeName})";
        var mode = b.RentalMode == RentalModes.SelfDrive ? "Tự lái" : "Có tài xế";

        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;max-width:640px;margin:0 auto;color:#0f172a\">");
        sb.Append("<div style=\"background:#0f1f3d;color:#fff;padding:16px 20px;border-radius:12px 12px 0 0\">")
          .Append("<div style=\"font-size:20px;font-weight:800\">DriveX</div>")
          .Append($"<div style=\"font-size:13px;color:#93c5fd\">{E(heading)}</div></div>");
        sb.Append("<div style=\"border:1px solid #e2e8f0;border-top:0;border-radius:0 0 12px 12px;padding:16px 20px\">");
        sb.Append($"<p style=\"margin:0 0 12px\"><strong>Số:</strong> {E(number)} &nbsp;·&nbsp; <strong>Ngày:</strong> {E(Vn(issuedUtc))}</p>");
        Section(sb, "Khách hàng", [
            ("Họ tên", b.Customer.User.FullName),
            ("Email", b.Customer.User.Email),
            ("Số điện thoại", b.Customer.User.Phone ?? "—")
        ]);
        Section(sb, "Đơn thuê", [
            ("Mã đơn", $"#{b.BookingId}"),
            ("Xe", vehicleText),
            ("Hình thức", mode),
            ("Nhận xe", $"{b.StartDate.ToString("dd/MM/yyyy HH:mm", Vi)} — {b.PickupAddress}"),
            ("Trả xe", $"{b.EndDate.ToString("dd/MM/yyyy HH:mm", Vi)} — {b.DropoffAddress}")
        ]);
        Section(sb, "Thanh toán", money);
        sb.Append($"<p style=\"margin:12px 0 0;font-size:12px;color:#64748b\">{E(note)}</p>");
        sb.Append("</div></div>");
        return sb.ToString();
    }

    private static void Section(StringBuilder sb, string title, IEnumerable<(string Label, string Value)> rows)
    {
        sb.Append($"<h3 style=\"font-size:14px;margin:14px 0 6px\">{E(title)}</h3>");
        sb.Append("<table style=\"width:100%;border-collapse:collapse;font-size:13px\">");
        foreach (var (label, value) in rows)
            sb.Append("<tr>")
              .Append($"<td style=\"padding:5px 0;color:#64748b;border-bottom:1px solid #f1f5f9\">{E(label)}</td>")
              .Append($"<td style=\"padding:5px 0;text-align:right;font-weight:600;border-bottom:1px solid #f1f5f9\">{E(value)}</td>")
              .Append("</tr>");
        sb.Append("</table>");
    }

    private static string E(string? value) => Enc.Encode(value ?? string.Empty);
    private static string Money(decimal amount) => amount.ToString("#,0", Vi) + " đ";
    private static string Vn(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Backend.Validation.VietnamTime.Zone)
            .ToString("dd/MM/yyyy HH:mm", Vi);

    public static string MethodLabel(string? method) => method switch
    {
        PaymentMethods.Cash => "Tiền mặt",
        PaymentMethods.BankTransfer => "Chuyển khoản",
        PaymentMethods.MoMo => "MoMo",
        PaymentMethods.VNPay => "VNPay",
        _ => method ?? "—"
    };
}
