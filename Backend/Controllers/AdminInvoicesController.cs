using Backend.Constants;
using Backend.Services.Invoices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>Admin: view and (re)send receipts / invoices when the first email failed or SMTP was not set.</summary>
[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/invoices")]
public class AdminInvoicesController(InvoiceService invoices, InvoiceMailService mail) : ControllerBase
{
    [HttpGet("booking/{bookingId:int}")]
    public async Task<ActionResult<IReadOnlyList<InvoiceDocumentResponse>>> ForBooking(int bookingId)
        => Ok((await invoices.GetForBookingAsync(bookingId)).Select(InvoiceDocumentResponse.From).ToList());

    [HttpPost("deposit/{paymentId:int}/send")]
    public async Task<ActionResult<InvoiceSendResponse>> SendDeposit(int paymentId)
        => Result(await mail.SendDepositReceiptAsync(paymentId));

    [HttpPost("trip/{bookingId:int}/send")]
    public async Task<ActionResult<InvoiceSendResponse>> SendTrip(int bookingId)
        => Result(await mail.SendTripInvoiceAsync(bookingId));

    private ActionResult<InvoiceSendResponse> Result(InvoiceSendResult r)
        => r.Found ? Ok(new InvoiceSendResponse(r.Sent, r.Message)) : NotFound(new { message = r.Message });
}

public record InvoiceSendResponse(bool Sent, string Message);

public record InvoiceDocumentResponse(string Kind, string Number, string Title, DateTime IssuedAt, string Html)
{
    public static InvoiceDocumentResponse From(InvoiceDocument d) => new(d.Kind, d.Number, d.Title, d.IssuedAtUtc, d.Html);
}
