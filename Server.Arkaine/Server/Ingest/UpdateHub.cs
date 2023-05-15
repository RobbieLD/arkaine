using Microsoft.AspNetCore.SignalR;

namespace Server.Arkaine.Ingest
{
    public class UpdateHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await Clients.Client(Context.ConnectionId).SendAsync("update", "Connected ...");
            await base.OnConnectedAsync();
        }
    }
}
