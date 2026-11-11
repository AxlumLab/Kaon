using System.IO.Compression;

namespace Kaon.Utils;

public abstract class GzipUtil
{
    public static byte[] CompressBytes(byte[] bytes)
    {
        using var ms = new MemoryStream();
        using (var cmpStream = new GZipStream(ms, CompressionMode.Compress, true))
            cmpStream.Write(bytes, 0, bytes.Length);
        return ms.ToArray();
    }
        
    public static async Task<byte[]> CompressBytesAsync(byte[] bytes)
    {
        using var ms = new MemoryStream();
        await using (var cmpStream = new GZipStream(ms, CompressionMode.Compress, true))
            cmpStream.Write(bytes, 0, bytes.Length);
        return ms.ToArray();
    }

        
    public static byte[] Decompress(byte[] bytes)
    {
        using var originalStream = new MemoryStream(bytes);
        using var decompressedStream = new MemoryStream();
        using (var decompressionStream = new GZipStream(originalStream, CompressionMode.Decompress))
        {
            decompressionStream.CopyTo(decompressedStream);
        }
        return decompressedStream.ToArray();
    }
    
    public static async Task<byte[]> DecompressAsync(byte[] bytes)
    {
        using var originalStream = new MemoryStream(bytes);
        using var decompressedStream = new MemoryStream();
        await using (var decompressionStream = new GZipStream(originalStream, CompressionMode.Decompress))
        {
            await decompressionStream.CopyToAsync(decompressedStream);
        }
        return decompressedStream.ToArray();
    }
}