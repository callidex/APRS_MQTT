using System.Net.Sockets;
using System.Text;

internal static class APRS
{

    public static string FormatCoordinates(double latitude, double longitude)
    {
        string latDirection = latitude >= 0 ? "N" : "S";
        string lonDirection = longitude >= 0 ? "E" : "W";

        latitude = Math.Abs(latitude);
        longitude = Math.Abs(longitude);

        int latDegrees = (int)latitude;
        int latMinutes = (int)((latitude - latDegrees) * 60);
        double latDecimalMinutes = (latitude - latDegrees) * 60 - latMinutes;

        int lonDegrees = (int)longitude;
        int lonMinutes = (int)((longitude - lonDegrees) * 60);
        double lonDecimalMinutes = (longitude - lonDegrees) * 60 - lonMinutes;

        return $"{latDegrees:00}{latMinutes:00}.{Math.Round(latDecimalMinutes * 1000):000}{latDirection}" +
               $"{lonDegrees:000}{lonMinutes:00}.{Math.Round(lonDecimalMinutes * 1000):000}{lonDirection}";
    }

    public static void SendAprsPacket(double latitude, double longitude, string callsign, string info)
    {
        string aprsServer = "rotate.aprs.net";
        int aprsPort = 14580;
        string aprsData = $"!{FormatCoordinates(latitude, longitude)} MeshBridge v1.0 {info}";
        string aprsPacket = $"{callsign}>APRS:!{aprsData}"; // Modify as per APRS packet structure

        try
        {
            using TcpClient client = new(aprsServer, aprsPort);
            using NetworkStream stream = client.GetStream();
            // Prepare the packet for sending
            byte[] data = Encoding.ASCII.GetBytes(aprsPacket + "\n");
            stream.Write(data, 0, data.Length);
            Console.WriteLine("Packet sent: " + aprsPacket);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}