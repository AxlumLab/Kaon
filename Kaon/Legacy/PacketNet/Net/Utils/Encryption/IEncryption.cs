namespace NetStickShared.Net.Utils.Encryption;

public enum EncryptionMethod : byte
{
    None = 0,
    Rsa = 1,
    Aes = 2
}

public interface IEncryption
{
    public EncryptionMethod Method { get; }
    public byte[] Pwd { get; set; }
    
    byte[] Encrypt(byte[] data);
    byte[] DeCrypt(byte[] data);
}