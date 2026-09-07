using Backend.DTOs.Realtime;
using Backend.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Backend.Services;

public class SignalRRealtimePublisher(IHubContext<RealtimeHub> hub, ILogger<SignalRRealtimePublisher> logger)
    : IRealtimePublisher
{
    public const string ClientMethod = "ReceiveEvent";

    public async Task PublishAsync(RealtimeEventDto evt, RealtimeAudience audience)
    {
        try
        {
            var tasks = new List<Task>(4);
            if (audience.CustomerId is int customerId)
                tasks.Add(hub.Clients.Group(RealtimeGroups.Customer(customerId)).SendAsync(ClientMethod, evt));
            if (audience.DriverId is int driverId)
                tasks.Add(hub.Clients.Group(RealtimeGroups.Driver(driverId)).SendAsync(ClientMethod, evt));
            if (audience.Operations)
            {
                tasks.Add(hub.Clients.Group(RealtimeGroups.Dispatcher).SendAsync(ClientMethod, evt));
                tasks.Add(hub.Clients.Group(RealtimeGroups.Admin).SendAsync(ClientMethod, evt));
            }

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Realtime publish failed for {EventType} {EntityId}", evt.EventType, evt.EntityId);
        }
    }
}
