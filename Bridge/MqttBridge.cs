
using Meshtastic.Protobufs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using MQTTnet.Server;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Google.Protobuf;

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
                if(packet?.Packet.Decoded!=null)
                { 
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
                else
                {
                    Console.WriteLine($"Encrypted packet from {packet.Packet.From.ToString("X")}");

                    string psk = "1PG7OiApB1nwvP+rz05pAQ==";
                    var dec = DecryptMeshtasticPayload(packet.Packet.Encrypted.ToByteArray(), Encoding.ASCII.GetBytes(psk) , new byte[16]);
                    Console.WriteLine(dec);
                    
                }



            }
            catch (InvalidProtocolBufferException ex)
            {
                message = "Invalid protocol message: " + ex.Message;
                Console.WriteLine(message);
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
            MeshPacket mp = serviceEnvelope.Packet;
            Data data = mp.Decoded;
            var user = Meshtastic.Protobufs.User.Parser.ParseFrom(data.Payload); 
            if(user!=null)
            {
                Console.WriteLine($"  {user.Id} {user.LongName} from {serviceEnvelope.Packet.From.ToString("X")}");
            }
            else
            {
                Console.WriteLine("  User is null");
            }
            var position = Position.Parser.ParseFrom(data.Payload);
            if(position != null)
            {
                Console.WriteLine($"  {position.LatitudeI}, {position.LongitudeI} from {serviceEnvelope.Packet.From.ToString("X")}");
            }
            else
            {
                Console.WriteLine("  Position is null");
            }

            Console.WriteLine("SE: " + serviceEnvelope.Packet.Decoded.Payload.ToString());
        }
    }

    private void PositionReport(ServiceEnvelope packet)
    {
        positions.RemoveAll(x => x.From == packet.Packet.From);
        positions.Add(new PositionInfo() { Position = Position.Parser.ParseFrom(packet.Packet.Decoded.Payload), From = packet.Packet.From });
    }

    private void NodeInfoReport(ServiceEnvelope packet)
    {
        if(nodeInfos.Where(x=>x.Packet.From == packet.Packet.From).Count() == 0) 
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

    public static byte[] DecryptMeshtasticPayload(byte[] ciphertext, byte[] key, byte[] iv)
    {
        try
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = key;
                aesAlg.IV = iv;

                // Set AES-256 properties
                aesAlg.Mode = CipherMode.CBC;  // Meshtastic uses CBC mode
                aesAlg.Padding = PaddingMode.PKCS7;  // Common padding mode

                // Create a decryptor to perform the stream transform.
                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for decryption.
                using (MemoryStream msDecrypt = new MemoryStream(ciphertext))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            // Read the decrypted bytes from the decrypting stream
                            // and place them into a string.
                            var decryptedText = srDecrypt.ReadToEnd();
                            return Encoding.UTF8.GetBytes(decryptedText);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error decrypting payload: {ex.Message}");
            return null;
        }
    }
}

