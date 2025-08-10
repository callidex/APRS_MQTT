
using Google.Protobuf.WellKnownTypes;
using Meshtastic.Protobufs;
using Microsoft.Extensions.Configuration;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Server;
using System.Xml.Linq;

public class MqttBridge
{
    private IMqttClient? MqttClient;
    readonly List<PositionInfo> positions = [];
    readonly List<Node> infoReportNodes = [];
    List<KnownNode> knownNodes = [];
    private MqttFactory factory = new MqttFactory();
    private IConfigurationRoot _Config = default!;
    public Task ConnectToMqttBrokerAsync(IConfigurationRoot config)
    {
        _Config = config;
        var whiteList = _Config.GetSection("whitelist").GetChildren().Select(x => x.Value).ToArray();
        return ConnectAsync();
    }
    Task<MqttClientConnectResult> ConnectAsync()
    {
        MqttClient = factory.CreateMqttClient();

        MqttClient.ConnectedAsync += (s) =>
        {
            Log("Connected to MQTT Broker.");
            MqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(_Config["topic"]).Build());
            return Task.CompletedTask;
        };

        MqttClient.ApplicationMessageReceivedAsync += e =>
        {
            try
            {
                if (e.ApplicationMessage.Topic != null && e.ApplicationMessage.Topic.Contains("json"))
                {
                    Log($"Json message {e.ApplicationMessage.Topic}");
                    return Task.CompletedTask;

                }
                if (e.ApplicationMessage.Topic != null && e.ApplicationMessage.Topic.Contains("stat"))
                {
                    Log($"Stat message {e.ApplicationMessage.Topic}");
                    return Task.CompletedTask;

                }

                var packet = ServiceEnvelope.Parser.ParseFrom(e.ApplicationMessage.PayloadSegment);
                if (packet?.Packet.Decoded != null)
                {
                    Log($"{packet.Packet.Decoded.Portnum} from {e.PacketIdentifier.ToString()} : {e.ApplicationMessage.Topic}");
                    switch (packet.Packet.Decoded.Portnum)
                    {
                        case PortNum.NodeinfoApp:
                            NodeInfoReport(packet);
                            ProcessNodes();
                            break;
                        case PortNum.PositionApp:
                            PositionReport(packet);
                            ProcessNodes();
                            break;

                        default:
                            break;
                    }
                }

            }
            catch (Exception ex)
            {
                Log($"Error processing message {ex.Message} {ex.StackTrace}");
            }
            return Task.CompletedTask;
        };

        MqttClient.DisconnectedAsync += async e =>
        {
            Log("Disconnected from MQTT Broker.");
            await Task.Delay(1000);
            await ConnectAsync();
        };
        var options = new MqttClientOptionsBuilder()
            .WithClientId(_Config["name"])
            .WithTcpServer(_Config["mqttserver"], 1883) // Replace with your MQTT broker address and port
            .WithCredentials(_Config["mqttuser"], _Config["mqttpass"]) // Optional: add your MQTT broker username and password if needed
            .WithCleanSession()
            .Build();

        // intialise known node
        var presets = _Config.GetSection("knownnodes").GetChildren().ToList();
        foreach (var n in presets)
        {
            knownNodes.Add(new KnownNode()
            {
                From = Convert.ToUInt32(n.GetSection("node").Value, 16),
                LongName = n.GetSection("id").ToString()!
            });
        }
        return MqttClient.ConnectAsync(options);
    }


    void Log(string message)
    {
        Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")} {message}");
    }


    private void ProcessNodes()
    {
        foreach (var node in infoReportNodes)
        {
            try
            {
                MeshPacket mp = node.ServiceEnvelope.Packet;
                Data data = mp.Decoded;
                var user = User.Parser.ParseFrom(data.Payload);
                if (user == null) continue;

                LogPositionAsync(node.ServiceEnvelope.Packet.From, user.LongName, user.HwModel.ToString());
            }
            catch (Exception ex)
            {
                Log($"Error processing node {node.From} {ex.Message}");
            }
        }
        foreach (var node in knownNodes)
        {
            LogPositionAsync(node.From, node.LongName);
        }
    }

    async Task LogPositionAsync(uint from, string longName, string hw = "Meshtastic")
    {
        var matchedPosition = positions.Where(x => x.From.ToString("X").ToLower() == from.ToString("X").ToLower()).FirstOrDefault();
        if (matchedPosition != null)
        {
            var name = longName.ToUpper().Split('/', '-', ' ')[0];
            var whiteList = _Config.GetSection("whitelist").GetChildren().Select(x => x.Value.ToUpper()).ToArray();
            if (!whiteList.Contains(name))
            {
                Log($"{name} found but not whitelisted");
                return;
            }
            Log($" MATCH  {name}  {matchedPosition.Position.LatitudeI}, {matchedPosition.Position.LongitudeI}");
            var callsign = $"{name}-12";
            await APRS.SendAprsPacketAsync(matchedPosition.Position.LatitudeI, matchedPosition.Position.LongitudeI, callsign, hw);
            positions.RemoveAll(x => x.From == from); // will only update next time a new position comes in
        }
    }



    private void PositionReport(ServiceEnvelope packet)
    {
        positions.RemoveAll(x => x.From == packet.Packet.From);
        var pos = new PositionInfo() { Position = Position.Parser.ParseFrom(packet.Packet.Decoded.Payload), From = packet.Packet.From };
        positions.Add(pos);
        Log($"Position {pos.Position.LatitudeI}, {pos.Position.LongitudeI} from {packet.Packet.From.ToString("X").ToLower()} ");
    }

    private void NodeInfoReport(ServiceEnvelope packet)
    {

        var node = new Node();
        node.ServiceEnvelope = packet;
        node.From = packet.Packet.From;
        infoReportNodes.RemoveAll(x => x.From == packet.Packet.From);
        infoReportNodes.Add(node);
        Log($"Node {packet.Packet.From.ToString("X").ToLower()}");
    }
}

