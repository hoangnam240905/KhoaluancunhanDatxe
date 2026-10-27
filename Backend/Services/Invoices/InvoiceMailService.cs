namespace Backend.Services.Invoices;

public sealed record InvoiceSendResult(bool Found, bool Sent, string Message);

/// <summary>
/// Emails a receipt / invoice to the booking's customer. A mail failure never bubbles up: payments and
/// trip completion are already committed; the document stays viewable on the web and admin can resend.
/// </summary>
public class InvoiceMailService(InvoiceService invoices, IEmailSender email, ILogger<InvoiceMailService> logger)
{
    public const string NotConfigured = "Email chưa được cấu hình (SMTP) — hóa đơn chỉ xem được trên web.";

    public async Task<InvoiceSendResult> SendDepositReceiptAsync(int paymentId, CancellationToken ct = default)
        => await SendAsync(await invoices.BuildDepositReceiptAsync(paymentId, ct), ct);

    public async Task<InvoiceSendResult> SendTripInvoiceAsync(int bookingId, CancellationToken ct = default)
        => await SendAsync(await invoices.BuildTripInvoiceAsync(bookingId, ct), ct);

    private async Task<InvoiceSendResult> SendAsync(InvoiceDocument? doc, CancellationToken ct)
    {
        if (doc is null)
            return new InvoiceSendResult(false, false, "Chưa có biên nhận / hóa đơn cho mục này.");
        if (string.IsNullOrWhiteSpace(doc.CustomerEmail))
            return new InvoiceSendResult(true, false, "Khách hàng chưa có email.");
        if (email is NullEmailSender)
        {
            logger.LogWarning("Invoice {Number} for booking {BookingId} not emailed: SMTP not configured.", doc.Number, doc.BookingId);
            return new InvoiceSendResult(true, false, NotConfigured);
        }

        try
        {
            await email.SendAsync(doc.CustomerEmail, $"{doc.Title} - DriveX", doc.Html, ct);
            logger.LogInformation("Invoice {Number} emailed for booking {BookingId}.", doc.Number, doc.BookingId);
            return new InvoiceSendResult(true, true, $"Đã gửi {doc.Title} tới email khách.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not email invoice {Number} for booking {BookingId}.", doc.Number, doc.BookingId);
            return new InvoiceSendResult(true, false, "Gửi email thất bại — hóa đơn vẫn xem được trên web, admin có thể gửi lại.");
        }
    }
}
