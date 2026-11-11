using Kaon.Protocol.Transform;

namespace Kaon.Protocol;

public static class FrameCodecBuilder
{
    /// <summary>
    /// 创建 tunnel 使用的 FrameCodec。
    /// Transform 顺序固定为：压缩 -> 加密。Decode 会由 pipeline 自动反向执行。
    /// compressionTransform 始终加入 pipeline，因为接收端需要根据 Header 的 Compressed 位自动解压。
    /// </summary>
    public static FrameCodec CreateTunnelCodec(
        CompressionFrameTransform compressionTransform,
        bool encryptionEnabled,
        string encryptionKey,
        int maxPayloadLength = 16 * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(compressionTransform);

        var transforms = new List<IFrameTransform> { compressionTransform };
        if (encryptionEnabled)
            transforms.Add(new EncryptionFrameTransform(ParseAes128Key(encryptionKey)));

        return new FrameCodec(
            new FrameTransformPipeline(transforms),
            new FrameCodecOptions { MaxPayloadLength = maxPayloadLength });
    }

    /// <summary>
    /// AES-128 固定使用 16 字节 key。当前阶段不做密钥交换，只允许配置提供。
    /// </summary>
    private static byte[] ParseAes128Key(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Tunnel encryption is enabled but encryption key is empty.");

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(key);
        }
        catch (FormatException)
        {
            bytes = System.Text.Encoding.UTF8.GetBytes(key);
        }

        if (bytes.Length != 16)
            throw new InvalidOperationException("AES-128 tunnel encryption key must be exactly 16 bytes.");

        return bytes;
    }
}
