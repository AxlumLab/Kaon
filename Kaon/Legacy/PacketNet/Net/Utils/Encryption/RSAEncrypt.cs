using System.Security.Cryptography;
using System.Text;

namespace NetStickShared.Net.Utils.Encryption;

public class RsaEncrypt : IEncryption
{
    public EncryptionMethod Method => EncryptionMethod.Rsa;
    public byte[] Pwd { get; set; }

    public RsaEncrypt(string base64Pwd)
    {
        Pwd = Convert.FromBase64String(base64Pwd);
    }

    public RsaEncrypt(byte[] pwd)
    {
        Pwd = pwd;
    }
    
    public static (string, string) GenRsaKey()
    {
        using var rsa = new RSACryptoServiceProvider();
        
        var publicKey = rsa.ToXmlString(false);
        var privateKey = rsa.ToXmlString(true);
        
        return (publicKey, privateKey);
    }

    public static (byte[], byte[]) ParseRsaKey((string, string) p)
    {
        return (Encoding.UTF8.GetBytes(p.Item1), Encoding.UTF8.GetBytes(p.Item2));
    }

    public static (string, string) ParseRsaByteKeyToBase64Key((byte[], byte[]) p)
    {
        return (Convert.ToBase64String(p.Item1), Convert.ToBase64String(p.Item2));
    }
    
    public byte[] Encrypt(byte[] data)
    {
        using var rsa = new RSACryptoServiceProvider();
        rsa.FromXmlString(Encoding.UTF8.GetString(Pwd));

        var encryptedData = rsa.Encrypt(data, false);
        return encryptedData;
    }

    
    public byte[] DeCrypt(byte[] data)
    {
        using var rsa = new RSACryptoServiceProvider();
        rsa.FromXmlString(Encoding.UTF8.GetString(Pwd));

        return rsa.Decrypt(data, false);
    }
}