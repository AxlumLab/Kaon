using System.Security.Cryptography;

namespace NetStickShared.Net.Utils.Encryption;

public class AesEncrypt : IEncryption
{
    public EncryptionMethod Method => EncryptionMethod.Aes;
    public byte[] Pwd { get; set; }
    public byte[]? Iv { get; set; }

    public AesEncrypt(string base64Pwd)
    {
        Pwd = Convert.FromBase64String(base64Pwd);
    }

    public AesEncrypt(byte[] pwd)
    {
        Pwd = pwd;
    }
    
    public void GenerateRandomIv()
    {
        var iv = new byte[16]; 
        // Assuming AES block size is 128 bits (16 bytes)

        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(iv);

        Iv = iv;
    }
    
    
    public byte[] Encrypt(byte[] data)
    {
        using var aesAlg = new AesGcm(Pwd);
        var iv = Iv;
        ArgumentNullException.ThrowIfNull(iv, "iv != null");

        var cipherText = new byte[data.Length];
        var tag = new byte[16]; // GCM authentication tag size is 16 bytes

        aesAlg.Encrypt(iv, data, cipherText, tag);

        // Combine IV, ciphertext, and tag
        var result = new byte[iv.Length + cipherText.Length + tag.Length];
        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
        Buffer.BlockCopy(cipherText, 0, result, iv.Length, cipherText.Length);
        Buffer.BlockCopy(tag, 0, result, iv.Length + cipherText.Length, tag.Length);

        return result;
    }

    public byte[] DeCrypt(byte[] data)
    {
        using var aesAlg = new AesGcm(Pwd);
        var iv = new byte[12]; // IV size for GCM is 12 bytes
        var cipherText = new byte[data.Length - iv.Length - 16];
        var tag = new byte[16]; // GCM authentication tag size is 16 bytes

        // Extract IV, ciphertext, and tag from the input
        Buffer.BlockCopy(data, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(data, iv.Length, cipherText, 0, cipherText.Length);
        Buffer.BlockCopy(data, iv.Length + cipherText.Length, tag, 0, tag.Length);

        // Decrypt
        var decryptedText = new byte[cipherText.Length];
        aesAlg.Decrypt(iv, cipherText, tag, decryptedText, null);

        return decryptedText;
    }
}