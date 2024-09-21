using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

public class MqttHub : Hub
{
    // Method to send the message to all connected clients
    public async Task SendMessage(string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", message);
    }
}
