using System.Security.Cryptography;

namespace Kaon.Protocol.Transform;

public sealed class EncryptionFrameTransform(ReadOnlySpan<byte> key) : IFrameTransform, IDisposable
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    // Keep separate instances so send and receive paths do not share mutable crypto state.
    private readonly AesGcm _encryptor = new(key, TagSize);
    private readonly AesGcm _decryptor = new(key, TagSize);

    public FrameTransformResult Encode(in FrameHeader header, OwnedBuffer? payload)
    {
        if ((header.Flags & FrameFlags.Encrypted) != 0)
            return new FrameTransformResult(header.Flags, payload, header.OriginalLength);

        var input = payload?.Memory ?? ReadOnlyMemory<byte>.Empty;
        var output = new OwnedBuffer(System.Buffers.MemoryPool<byte>.Shared.Rent(NonceSize + TagSize + input.Length));
        try
        {
            var outputSpan = output.WritableMemory.Span;
            var nonce = outputSpan[..NonceSize];
            var tag = outputSpan.Slice(NonceSize, TagSize);
            var ciphertext = outputSpan[(NonceSize + TagSize)..(NonceSize + TagSize + input.Length)];

            // 每个 frame 使用独立 nonce，避免同一密钥下 nonce 复用导致 GCM 失效。
            RandomNumberGenerator.Fill(nonce);
            Span<byte> aad = stackalloc byte[FrameHeader.Size];
            WriteAssociatedData(in header, aad);
            _encryptor.Encrypt(nonce, input.Span, ciphertext, tag, aad);
            output.SetLength(NonceSize + TagSize + input.Length);

            return new FrameTransformResult(header.Flags | FrameFlags.Encrypted, output, header.OriginalLength);
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }

    public FrameTransformResult Decode(in FrameHeader header, OwnedBuffer? payload)
    {
        if ((header.Flags & FrameFlags.Encrypted) == 0)
            return new FrameTransformResult(header.Flags, payload, header.OriginalLength);

        var input = payload?.Memory ?? ReadOnlyMemory<byte>.Empty;
        if (input.Length < NonceSize + TagSize)
            throw new InvalidDataException("Encrypted frame payload is too small.");

        var nonce = input.Span[..NonceSize];
        var tag = input.Span.Slice(NonceSize, TagSize);
        var ciphertext = input.Span[(NonceSize + TagSize)..];
        var plaintext = new OwnedBuffer(System.Buffers.MemoryPool<byte>.Shared.Rent(ciphertext.Length));

        try
        {
            Span<byte> aad = stackalloc byte[FrameHeader.Size];
            WriteAssociatedData(in header, aad);
            _decryptor.Decrypt(nonce, ciphertext, tag, plaintext.WritableMemory.Span[..ciphertext.Length], aad);
            plaintext.SetLength(ciphertext.Length);

            return new FrameTransformResult(header.Flags & ~FrameFlags.Encrypted, plaintext, header.OriginalLength);
        }
        catch
        {
            plaintext.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        _encryptor.Dispose();
        _decryptor.Dispose();
    }

    private static void WriteAssociatedData(in FrameHeader header, Span<byte> buffer)
    {
        var associatedHeader = new FrameHeader(
            header.Type,
            header.StreamId,
            0,
            header.Flags & ~FrameFlags.Encrypted,
            header.Version);
        associatedHeader.WriteTo(buffer);
    }
}
