

using Boerman.AprsClient;
using Meshtastic.Protobufs;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using MQTTnet.Server;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

public class MqttMessagesModel : PageModel
{
    private static IMqttClient _mqttClient;
    private static List<string> _messages = new List<string>();
    private static Listener _listener;
    private readonly IHubContext<MqttHub> _hubContext;

    public MqttMessagesModel(IHubContext<MqttHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public List<string> Messages { get; set; }

    public async Task OnGetAsync()
    {
        // If the client is not connected, connect to the MQTT broker
        if (_mqttClient == null || !_mqttClient.IsConnected)
        {
            await ConnectToMqttBroker();
        }

        // Display the list of messages in the page
        Messages = _messages;

        _listener = new Listener();

        
        _listener.PacketReceived += (sender, packet) =>
        {
            var message = packet.ToString();
            _messages.Add($"APRS {message}");
            _hubContext.Clients.All.SendAsync("ReceiveMessage", message);
        };  

    }

    // Connect to the MQTT broker and subscribe to the topic
    private async Task ConnectToMqttBroker()
    {
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithClientId("VK4PLY_HACK")
            .WithTcpServer("lightning.vk4ya.com", 1883) // Replace with your MQTT broker address and port
            .WithCredentials("BDARS", "lightning") // Optional: add your MQTT broker username and password if needed
            .WithCleanSession()
            .Build();


        // Connect to the broker
        
        var topic = "msh/bdars/2/e/LongFast/!bridge";
        topic = "msh/bdars/2/e/LongFast/#";

        _mqttClient.ConnectedAsync += async (s) =>
        {
            var message = "Connected to MQTT Broker.";
            _messages.Add(message);

            // Broadcast the message to all clients via SignalR
            _hubContext.Clients.All.SendAsync("ReceiveMessage", message);
            await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).Build());
        };


        _mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            var message = "Unknown message.";

            // decode it
            try
            {

                var packet = Meshtastic.Protobufs.ServiceEnvelope.Parser.ParseFrom(e.ApplicationMessage.Payload);
                message = packet.ToString();
                message = packet.Packet.Decoded.Portnum.ToString() + message;
                switch (packet.Packet.Decoded.Portnum)
                {
                    case PortNum.NodeinfoApp:
                        message = NodeInfoReport(packet); break;

                    case PortNum.TextMessageApp:
                        var textMessage = Meshtastic.Protobufs.Data.Parser.ParseFrom(packet.Packet.Decoded.Payload);
                        message = $"TextMessage: {textMessage.Payload.ToString()}";
                        break;

                    case PortNum.PositionApp:
                        message = PositionReport(packet); break;

                    case PortNum.NeighborinfoApp:
                        var neighborInfo = Meshtastic.Protobufs.NeighborInfo.Parser.ParseFrom(packet.Packet.Decoded.Payload);
                        message = $"NeighborInfo: {neighborInfo.NodeId} {string.Join(",", neighborInfo.Neighbors.ToList())}";
                        break;
                    case PortNum.TelemetryApp:
                        var telemetry = Telemetry.Parser.ParseFrom(packet.Packet.Decoded.Payload);
                        if(telemetry.EnvironmentMetrics != null)
                            message = $"Telemetry: press {telemetry.EnvironmentMetrics.BarometricPressure} temp: {telemetry.EnvironmentMetrics.Temperature}";
                        break;
                    case PortNum.TextMessageCompressedApp:
                        var textMessageCompressed = Meshtastic.Protobufs.Data.Parser.ParseFrom(packet.Packet.Decoded.Payload);
                        message = $"TextMessageCompressed: {textMessageCompressed.Payload.ToString()}";
                        break;
                    case PortNum.WaypointApp:
                        var waypoint = Waypoint.Parser.ParseFrom(packet.Packet.Decoded.Payload);
                        message = $"Waypoint: {waypoint.LatitudeI}, {waypoint.LongitudeI}";
                        break;
                    default:
                        message = $"Unhandled: " + message ;
                        break;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                if (ex.InnerException != null)
                {
                    var innerMessage = ex.InnerException.Message;
                    message += " Inner Exception: " + innerMessage;
                }
                _messages.Add(message);
            }   

            // Add the message to the list
            _messages.Add(message);
            // Broadcast the message to all clients via SignalR
            _hubContext.Clients.All.SendAsync("ReceiveMessage", message);

            return Task.CompletedTask;
        };

        _mqttClient.DisconnectedAsync += e =>
        {
            var message = "Disconnected from MQTT Broker.";
            _messages.Add(message);

            // Broadcast the message to all clients via SignalR
            _hubContext.Clients.All.SendAsync("ReceiveMessage", message);
            return Task.CompletedTask;
        };

        var result = await _mqttClient.ConnectAsync(options);
        if (result.ResultCode != MqttClientConnectResultCode.Success) return;


        var message = new MqttApplicationMessageBuilder()
                .WithTopic("msh/bdars/2/e/LongFast/!bridge")
                .WithPayload($"APRS bridge sign in {DateTime.Now:hh-mm-ss}")
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag()
        .Build();

        await _mqttClient.PublishAsync(message);

    }

    private static string PositionReport(ServiceEnvelope packet)
    {
        string message;
        var position = Meshtastic.Protobufs.Position.Parser.ParseFrom(packet.Packet.Decoded.Payload);
        message = $"Position: {position.LatitudeI}, {position.LongitudeI} {position.Altitude} {position.SatsInView} ch: {packet.ChannelId} {packet.Packet.From}";
        return message;
    }

    private static string NodeInfoReport(ServiceEnvelope packet)
    {
        string message;
        var nodeInfo = NodeInfo.Parser.ParseFrom(packet.Packet.Decoded.Payload);
        message = $"NodeInfo: {nodeInfo.Position.LatitudeI}, {nodeInfo.Position.LongitudeI}";
        return message;
    }
}
