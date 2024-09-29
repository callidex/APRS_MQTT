using System.Net.Sockets;
using System.Text;

public static class APRS
{
    public static string FormatCoordinates(double inLat, double inLong)
    {

        string latDirection = inLat >= 0 ? "N" : "S";
        string lonDirection = inLong >= 0 ? "E" : "W";


        var latitude = Math.Abs(inLat);
        var longitude = Math.Abs(inLong);



        int latdegrees = (int)(latitude / 10000000);
        int londegrees = (int)(longitude / 10000000);



        int latfraction = ((int)(latitude % 10000000));
        int lonfraction = ((int)(longitude % 10000000));

        float latminutes = (float)latfraction * (60.0f / 1000000.0f);
        float lonminutes = (float)lonfraction * (60.0f / 1000000.0f);

        return $"{latdegrees}{latminutes.ToString().Substring(0, 2)}.{Math.Round(latminutes * 1000).ToString().Substring(0, 2)}{latDirection}" +
               $"/{londegrees}{lonminutes.ToString().Substring(0, 2)}.{Math.Round(lonminutes * 1000).ToString().Substring(0, 2)}{lonDirection}";
    }

    //Unused, but in case we want to create the login on the fly
    private static int GenerateAPRSLogin(string callsign)
    {
        callsign = callsign.ToUpper();
        int hash = 0x73E2;

        foreach (char c in callsign)
        {
            hash ^= (c << 8);
            for (int i = 0; i < 8; i++)
            {
                if ((hash & 0x8000) != 0)
                {
                    hash = (hash << 1) ^ 0x1021;
                }
                else
                {
                    hash <<= 1;
                }
            }
        }
        return hash & 0x7FFF;
    }

    public async static Task SendAprsPacketAsync(double latitude, double longitude, string callsign, string info)
    {
        string passcode = "23968";
        string softwareName = "VK4PLY_MeshAprs";
        string softwareVersion = "1.0";

        string aprsServer = "perth.aprs2.net";
        int aprsPort = 14580;
        string aprsData = $"{FormatCoordinates(latitude, longitude)}./A=00024 MeshBridge v1.0 {info}";
        string aprsPacket = $"{callsign}>APDR15,WIDE1-1:={aprsData}"; // Modify as per APRS packet structure
        string loginMessage = $"user VK4PLY-12 pass {passcode} vers {softwareName} {softwareVersion}\n";
        try
        {
            using TcpClient client = new(aprsServer, aprsPort);
            using NetworkStream stream = client.GetStream();
            byte[] data = Encoding.ASCII.GetBytes(loginMessage + "\n");
            await stream.WriteAsync(data, 0, data.Length);
            byte[] buffer = new byte[1024];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            string response = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.WriteLine(aprsPacket);
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