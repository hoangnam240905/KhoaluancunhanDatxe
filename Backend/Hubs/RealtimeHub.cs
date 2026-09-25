using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Backend.Hubs;

[Authorize]
public class RealtimeHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var role = Context.User?.FindFirstValue(ClaimTypes.Role);
        if (!int.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            Context.Abort();
            return;
        }

        switch (role)
        {
            case RoleNames.Customer:
                await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Customer(userId));
                break;
            case RoleNames.Driver:
                await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Driver(userId));
                break;
            case RoleNames.Dispatcher:
                await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Dispatcher);
                break;
            case RoleNames.Admin:
                await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Admin);
                break;
            default:
                Context.Abort();
                return;
        }

        await base.OnConnectedAsync();
    }
}
