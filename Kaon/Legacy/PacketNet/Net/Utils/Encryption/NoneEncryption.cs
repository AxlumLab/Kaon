namespace NetStickShared.Net.Utils.Encryption;

public class NoneEncryption : IEncryption
{
    public EncryptionMethod Method => EncryptionMethod.None;
    public byte[] Pwd { get; set; } = [];

    public byte[] Encrypt(byte[] data) => data;
    public byte[] DeCrypt(byte[] data) => data;
}