
using Meshtastic.Protobufs;
using Microsoft.Extensions.Configuration;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Server;

public class MqttBridge
{
    private IMqttClient? MqttClient;
    readonly List<PositionInfo> positions = new List<PositionInfo>();
    readonly List<ServiceEnvelope> nodeInfos = new List<ServiceEnvelope>();

    public void ConnectToMqttBroker(IConfigurationRoot config)
    {
        var topic = config["topic"];
        MqttClient = CreateClient(topic);
        var options = new MqttClientOptionsBuilder()
            .WithClientId(config["name"])
            .WithTcpServer(config["mqttserver"], 1883) // Replace with your MQTT broker address and port
            .WithCredentials(config["mqttuser"], config["mqttpass"]) // Optional: add your MQTT broker username and password if needed
            .WithCleanSession()
            .Build();
        var result = MqttClient.ConnectAsync(options).Result;
    }

    private IMqttClient CreateClient(string topic)
    {
        var factory = new MqttFactory();
        MqttClient = factory.CreateMqttClient();
        MqttClient.ConnectedAsync += (s) =>
        {
            Console.WriteLine("Connected to MQTT Broker.");
            MqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).Build());
            return Task.CompletedTask;
        };

        MqttClient.ApplicationMessageReceivedAsync += e =>
        {
            try
            {
                var packet = ServiceEnvelope.Parser.ParseFrom(e.ApplicationMessage.PayloadSegment);
                if (packet?.Packet.Decoded != null)
                {
                    switch (packet.Packet.Decoded.Portnum)
                    {
                        case PortNum.NodeinfoApp:
                            NodeInfoReport(packet);
                            DumpInfo();
                            break;
                        case PortNum.PositionApp:
                            PositionReport(packet);
                            DumpInfo();
                            break;

                        default:
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
            }
            return Task.CompletedTask;
        };

        MqttClient.DisconnectedAsync += e =>
        {
            Console.WriteLine("Disconnected from MQTT Broker.");
            return Task.CompletedTask;
        };
        return MqttClient;
    }

    private void DumpInfo()
    {
        foreach (var serviceEnvelope in nodeInfos)
        {
            MeshPacket mp = serviceEnvelope.Packet;
            Data data = mp.Decoded;
            var user = User.Parser.ParseFrom(data.Payload);
            if (user == null) continue;
            var matchedPosition = positions.Where(x => x.From.ToString("X").ToLower() == serviceEnvelope.Packet.From.ToString("X").ToLower()).FirstOrDefault();
            if (matchedPosition != null)
            {
                var name = user.LongName.Split(  '/', '-' )[0];
                Console.WriteLine(name);
                if (name.ToLower().StartsWith("vk4") &&
                        name.Length > 4 &&
                        name.Length < 7)
                {
                    Console.WriteLine($" MATCH  {user.Id} {name}  {matchedPosition.Position.LatitudeI}, {matchedPosition.Position.LongitudeI}");
                    var callsign = $"{name}-12";
                    APRS.SendAprsPacketAsync(matchedPosition.Position.LatitudeI, matchedPosition.Position.LongitudeI, "VK4PLY-12", user.HwModel.ToString());
                }
            }
        }
    }

    private void PositionReport(ServiceEnvelope packet)
    {
        positions.RemoveAll(x => x.From == packet.Packet.From);
        positions.Add(new PositionInfo() { Position = Position.Parser.ParseFrom(packet.Packet.Decoded.Payload), From = packet.Packet.From });
    }

    private void NodeInfoReport(ServiceEnvelope packet)
    {
        if (nodeInfos.Where(x => x.Packet.From == packet.Packet.From).Count() == 0)
            nodeInfos.Add(packet);
    }
}

