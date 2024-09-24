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

    public async static Task SendAprsPacketAsync(double latitude, double longitude, string callsign, string info)
    {
        string passcode = "23968";
        string softwareName = "VK4PLY_MeshAprs";
        string softwareVersion = "1.0";

        string aprsServer = "rotate.aprs.net";
        int aprsPort = 14580;
        string aprsData = $"!{FormatCoordinates(latitude, longitude)} MeshBridge v1.0 {info}";
        string aprsPacket = $"{callsign}>APRS:{aprsData}"; // Modify as per APRS packet structure
        string loginMessage = $"user {callsign} pass {passcode} vers {softwareName} {softwareVersion}\n";
        try
        {
            using TcpClient client = new(aprsServer, aprsPort);
            using NetworkStream stream = client.GetStream();
            byte[] data = Encoding.ASCII.GetBytes(loginMessage + "\n");
            await stream.WriteAsync(data, 0, data.Length);
            byte[] buffer = new byte[1024]; 
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            string response = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.WriteLine(response);

            data = Encoding.ASCII.GetBytes(aprsPacket + "\n");
            await stream.WriteAsync(data, 0, data.Length);
            buffer = new byte[1024];
            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            response = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.WriteLine(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Login Failed: " + ex.Message);
        }
    }
}