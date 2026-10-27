using System.Threading.Channels;
using Backend.Constants;
using Backend.DTOs.Realtime;

namespace Backend.Services.Invoices;

/// <summary>
/// Paid deposits and completed trips arrive here from the realtime publisher. Like the notification
/// queue, events are published inside the SqliteWriteLock transaction, so mail is sent later by
/// <see cref="InvoiceMailWriter"/> after the business change has committed (and never holds the lock).
/// </summary>
public sealed class InvoiceMailQueue
{
    private readonly Channel<RealtimeEventDto> _channel = Channel.CreateBounded<RealtimeEventDto>(
        new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public static bool IsRelevant(RealtimeEventDto evt)
        => (evt.EventType == RealtimeEventTypes.PaymentStatusChanged && Status(evt) == PaymentStatuses.Paid)
           || (evt.EventType == RealtimeEventTypes.BookingStatusChanged && Status(evt) == BookingStatuses.Completed
               && evt.BookingId is not null);

    public bool TryEnqueue(RealtimeEventDto evt) => _channel.Writer.TryWrite(evt);

    public ChannelReader<RealtimeEventDto> Reader => _channel.Reader;

    private static string? Status(RealtimeEventDto evt)
        => evt.Payload is not null && evt.Payload.TryGetValue("status", out var s) ? s?.ToString() : null;
}

/// <summary>Background sender: deposit receipt for a Paid payment, trip invoice for a Completed booking.</summary>
public sealed class InvoiceMailWriter(
    InvoiceMailQueue queue,
    IServiceScopeFactory scopes,
    ILogger<InvoiceMailWriter> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var evt in queue.Reader.ReadAllAsync(stoppingToken))
            {
                // The event is raised before the business transaction commits: wait, and retry while the
                // payment / booking is not yet visible as Paid / Completed (or the database is still locked).
                for (var attempt = 1; attempt <= 5; attempt++)
                {
                    await Task.Delay(300 * attempt, stoppingToken);
                    try
                    {
                        using var scope = scopes.CreateScope();
                        var mail = scope.ServiceProvider.GetRequiredService<InvoiceMailService>();
                        var result = evt.EventType == RealtimeEventTypes.PaymentStatusChanged
                            ? await mail.SendDepositReceiptAsync(evt.EntityId, stoppingToken)
                            : await mail.SendTripInvoiceAsync(evt.BookingId!.Value, stoppingToken);
                        if (result.Found) break;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        if (attempt == 5)
                            logger.LogWarning(ex, "Invoice mail failed for {EventType} {EntityId}.", evt.EventType, evt.EntityId);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }
}
