using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Safar.Api.Hubs;

/// <summary>Real-time channel for the admin portal (new bookings, cancellations).</summary>
[Authorize(Policy = "Staff")]
public class NotificationsHub : Hub
{
    public const string StaffGroup = "staff";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, StaffGroup);
        await base.OnConnectedAsync();
    }
}
