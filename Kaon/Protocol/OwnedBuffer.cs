using System.Buffers;

namespace Kaon.Protocol;

public sealed class OwnedBuffer : IDisposable
{
    private IMemoryOwner<byte>? _owner;

    public OwnedBuffer(IMemoryOwner<byte> owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        FrameMetrics.AddPoolRent(owner.Memory.Length);
    }

    public int Length { get; private set; }

    public int Capacity => GetOwner().Memory.Length;

    public Memory<byte> WritableMemory => GetOwner().Memory;

    public ReadOnlyMemory<byte> Memory => GetOwner().Memory[..Length];

    public bool HasCapacity(int requiredLength)
    {
        return (uint)requiredLength <= (uint)GetOwner().Memory.Length;
    }

    public bool TrySetLength(int length)
    {
        if (!HasCapacity(length))
            return false;

        Length = length;
        return true;
    }

    public void SetLength(int length)
    {
        if (!TrySetLength(length))
            throw new ArgumentOutOfRangeException(nameof(length));
    }

    private IMemoryOwner<byte> GetOwner()
    {
        return _owner ?? throw new ObjectDisposedException(nameof(OwnedBuffer));
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _owner, null)?.Dispose();
    }
}
