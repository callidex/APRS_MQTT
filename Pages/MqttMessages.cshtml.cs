

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

        //// Set up event handler for receiving messages
        //_mqttClient.UseConnectedHandler(async e =>
        //    {
        //        Console.WriteLine("Connected successfully with MQTT Broker.");
        //               //        // Subscribe to the topic
        //        await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("msh/bdars/2/e/LongFast").Build());
        //        Console.WriteLine("Subscribed to topic msh/bdars/2/e/LongFast");
        //    });

        //_mqttClient.UseDisconnectedHandler(e =>
        //{
        //    Console.WriteLine("Disconnected from MQTT Broker.");
        //});

        //_mqttClient.UseApplicationMessageReceivedHandler(async e =>
        //{
        //    var message = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
        //    Console.WriteLine($"Received message: {message}");

        //    // Add the message to the list
        //    _messages.Add(message);

        //    // Broadcast the message to all clients via SignalR
        //    await _hubContext.Clients.All.SendAsync("ReceiveMessage", message);
        //});


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
            var message = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

            // decode it
            try
            {
                var packet = Meshtastic.Protobufs.ServiceEnvelope.Parser.ParseFrom(e.ApplicationMessage.Payload);

                switch (packet.Packet.Decoded.Portnum)
                {
                    case PortNum.NodeinfoApp:
                        var nodeInfo = NodeInfo.Parser.ParseFrom(packet.Packet.Decoded.Payload);
                        message = $"NodeInfo: {nodeInfo.Position.LatitudeI}, {nodeInfo.Position.LongitudeI}";
                        break;

                    case PortNum.TextMessageApp:
                        var textMessage = Meshtastic.Protobufs.Data.Parser.ParseFrom(packet.Packet.Decoded.Payload);
                        message = $"TextMessage: {textMessage.Payload.ToString()}";
                        break;
                    
                }


                message = packet.ToString();
            }
            catch (Exception ex)
            {
                message = ex.Message;
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
                .WithTopic(topic)
                .WithPayload($"APRS bridge sign in {DateTime.Now:hh-mm-ss}")
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag()
        .Build();

//        await _mqttClient.PublishAsync(message);

    }
}
