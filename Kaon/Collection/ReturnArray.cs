using System.Buffers;
using System.Runtime.CompilerServices;

namespace Kaon.Collection;

public readonly struct RentedArray<T> : IDisposable
{
    /// <summary>
    /// Number of <see cref="T"/> elements rented
    /// </summary>
    public int Length { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; }
    private readonly ArrayPool<T> _pool;
    private readonly T[] _array;

    /// <summary>
    /// A <see cref="System.Span{T}"/> representation of the array
    /// </summary>
    public Span<T> Span => _array.AsSpan(0, Length);

    /// <summary>
    /// A <see cref="System.Memory{T}"/> representation of the array
    /// </summary>
    public Memory<T> Memory => _array.AsMemory(0, Length);

    public RentedArray(int length, ArrayPool<T>? pool = null)
    {
        this._pool = pool ?? ArrayPool<T>.Shared;
        _array = this._pool.Rent(length);
        Length = length;
    }

    /// <summary>
    /// Returns the rented array to the pool
    /// </summary>
    public void Dispose() => _pool.Return(_array);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Span<T>(in RentedArray<T> rentedArray) => rentedArray.Span;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<T>(in RentedArray<T> rentedArray) => rentedArray.Span;


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Memory<T>(in RentedArray<T> rentedArray) => rentedArray.Memory;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlyMemory<T>(in RentedArray<T> rentedArray) => rentedArray.Memory;
}