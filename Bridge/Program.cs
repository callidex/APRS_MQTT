internal partial class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Bridge Starting");
        // Create a new thread to run the MQTT client
        MqttBridge bridge = new MqttBridge();
        bridge.ConnectToMqttBroker();
        Console.ReadKey();
    }
}

