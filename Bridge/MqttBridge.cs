
using Meshtastic.Protobufs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using MQTTnet.Server;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

public class PositionInfo
{
    // For Bob: Proto buf version is sealed so can't inherit
    public Position Position { get; set; }
    public uint From { get; set; }
}

public class MqttBridge
{
    private IMqttClient? MqttClient;
    readonly List<PositionInfo> positions = new List<PositionInfo>();
    readonly List<ServiceEnvelope> nodeInfos = new List<ServiceEnvelope>();

    public void ConnectToMqttBroker()
    {

        var config = new ConfigurationBuilder().AddJsonFile("config.json").Build();

        var topic = config["topic"];

        var factory = new MqttFactory();
        MqttClient = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithClientId(config["name"])
            .WithTcpServer(config["mqttserver"], 1883) // Replace with your MQTT broker address and port
            .WithCredentials(config["mqttuser"], config["mqttpass"]) // Optional: add your MQTT broker username and password if needed
            .WithCleanSession()
            .Build();

        MqttClient.ConnectedAsync += (s) =>
        {
            Console.WriteLine("Connected to MQTT Broker.");

            var message = new MqttApplicationMessageBuilder()
                    .WithTopic(config["publishtopic"])
                    .WithPayload($"APRS bridge sign in {DateTime.Now:hh-mm-ss}")
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag()
            .Build();

            //MqttClient.PublishAsync(message);

            MqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).Build());


            return Task.CompletedTask;
        };

        MqttClient.ApplicationMessageReceivedAsync += e =>
        {
            var message = "Unknown message.";
            Console.WriteLine(e.ApplicationMessage.Topic);

            // decode it
            try
            {
                var packet = Meshtastic.Protobufs.ServiceEnvelope.Parser.ParseFrom(e.ApplicationMessage.PayloadSegment);
                Console.WriteLine(packet.ToString());
                switch (packet.Packet.Decoded.Portnum)
                {
                    case PortNum.NodeinfoApp:
                        Console.WriteLine("NodeInfoApp");
                        NodeInfoReport(packet);
                        DumpInfo();
                        break;
                    case PortNum.PositionApp:
                        Console.WriteLine("PositionReport");
                        PositionReport(packet);
                        DumpInfo();
                        break;

                    default:
                        Console.WriteLine($"Unhandled packet {packet.Packet.Decoded.Portnum}");
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
                Console.WriteLine(message);
            }


            return Task.CompletedTask;
        };

        MqttClient.DisconnectedAsync += e =>
        {
            Console.WriteLine("Disconnected from MQTT Broker.");
            return Task.CompletedTask;
        };

        var result = MqttClient.ConnectAsync(options).Result;
        if (result.ResultCode != MqttClientConnectResultCode.Success) return;
    }

    private void DumpInfo()
    {
        Console.WriteLine("Positions:");
        foreach (var position in positions)
        {
            Console.WriteLine($"  {position.Position.LatitudeI}, {position.Position.LongitudeI} {position.From.ToString("X")}");
        }

        Console.WriteLine("NodeInfos:");
        foreach (var serviceEnvelope in nodeInfos)
        {
            Console.WriteLine("NODE INFO ON " + serviceEnvelope.ChannelId);
            Console.WriteLine("NODE INFO ON FROM " + serviceEnvelope.Packet.From.ToString("X"));
            try
            {
                Console.WriteLine(MyToString(serviceEnvelope.Packet));
                Console.WriteLine(serviceEnvelope.ToString());
                var nodeInfo = NodeInfo.Parser.ParseFrom(serviceEnvelope.Packet.Decoded.Payload);
                Console.WriteLine($" {nodeInfo.User.Id} {nodeInfo.User.LongName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error parsing node info: " + ex.Message);
            }
            Console.WriteLine("SE: " + serviceEnvelope.Packet.Decoded.Payload.ToString());
        }
    }

    private void PositionReport(ServiceEnvelope packet)
    {
        positions.Add(new PositionInfo() { Position = Position.Parser.ParseFrom(packet.Packet.Decoded.Payload), From = packet.Packet.From });
    }

    private void NodeInfoReport(ServiceEnvelope packet)
    {
        nodeInfos.Add(packet);
    }
    public string MyToString(object o)
    {
        return GetType().GetProperties()
            .Select(info => (info.Name, Value: info.GetValue(o, null) ?? "(null)"))
            .Aggregate(
                new StringBuilder(),
                (sb, pair) => sb.AppendLine($"{pair.Name}: {pair.Value}"),
                sb => sb.ToString());
    }
}

