using System.Net;

namespace NetStickShared.Net.Utils;

public static class ConnectionCode
{
    public static string Serialize(string ip, int port)
    {
        var ipAddress = IPAddress.Parse(ip);
        var ipBytes = ipAddress.GetAddressBytes();
        
        var connectionCode = ((long)BitConverter.ToInt32(ipBytes, 0) << 16) | (uint)(port & 0xFFFF);
        return Convert.ToHexString(BitConverter.GetBytes(connectionCode));
    }

    public static (string ip, int port) Deserialize(string connectionCode)
    {
        var code = BitConverter.ToInt64(Convert.FromHexString(connectionCode));
        
        var ipInt = (int)(code >> 16);
        var port = (int)(code & 0xFFFF);
        
        var ip = new IPAddress(BitConverter.GetBytes(ipInt)).ToString();
        return (ip, port);
    }
}