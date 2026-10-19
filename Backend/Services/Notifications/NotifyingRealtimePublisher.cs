using Backend.DTOs.Realtime;

namespace Backend.Services.Notifications;

/// <summary>
/// Wraps the SignalR publisher: sends the realtime event as before, then queues it for the
/// notification inbox. One hook covers every flow that already publishes (create, cancel, assign,
/// handover, driver accept/start/complete, incident) without touching those services.
/// </summary>
public sealed class NotifyingRealtimePublisher(IRealtimePublisher inner, NotificationQueue queue) : IRealtimePublisher
{
    public async Task PublishAsync(RealtimeEventDto evt, RealtimeAudience audience)
    {
        await inner.PublishAsync(evt, audience);
        if (NotificationRules.IsRelevant(evt))
            queue.TryEnqueue(evt);
    }
}
