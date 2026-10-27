using Backend.DTOs.Realtime;
using Backend.Services.Invoices;

namespace Backend.Services.Notifications;

/// <summary>
/// Wraps the SignalR publisher: sends the realtime event as before, then queues it for the
/// notification inbox. One hook covers every flow that already publishes (create, cancel, assign,
/// handover, driver accept/start/complete, incident) without touching those services.
/// Also feeds the invoice mail queue (paid deposit → receipt, completed trip → invoice).
/// </summary>
public sealed class NotifyingRealtimePublisher(
    IRealtimePublisher inner, NotificationQueue queue, InvoiceMailQueue? invoices = null) : IRealtimePublisher
{
    public async Task PublishAsync(RealtimeEventDto evt, RealtimeAudience audience)
    {
        await inner.PublishAsync(evt, audience);
        if (NotificationRules.IsRelevant(evt))
            queue.TryEnqueue(evt);
        if (invoices is not null && InvoiceMailQueue.IsRelevant(evt))
            invoices.TryEnqueue(evt);
    }
}
