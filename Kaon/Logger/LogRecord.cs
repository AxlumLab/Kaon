namespace Kaon.Logger;

public readonly struct LogArguments
{
    private readonly object? _arg0;
    private readonly object? _arg1;
    private readonly object? _arg2;
    private readonly object? _arg3;
    private readonly object? _arg4;
    private readonly object? _arg5;
    private readonly object?[]? _array;

    private LogArguments(
        int count,
        object? arg0 = null,
        object? arg1 = null,
        object? arg2 = null,
        object? arg3 = null,
        object? arg4 = null,
        object? arg5 = null,
        object?[]? array = null)
    {
        Count = count;
        _arg0 = arg0;
        _arg1 = arg1;
        _arg2 = arg2;
        _arg3 = arg3;
        _arg4 = arg4;
        _arg5 = arg5;
        _array = array;
    }

    public static LogArguments Empty => default;
    public int Count { get; }

    public object? this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (_array != null)
                return _array[index];

            return index switch
            {
                0 => _arg0,
                1 => _arg1,
                2 => _arg2,
                3 => _arg3,
                4 => _arg4,
                5 => _arg5,
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };
        }
    }

    public static LogArguments Create<T0>(T0 arg0)
        => new(1, arg0);

    public static LogArguments Create<T0, T1>(T0 arg0, T1 arg1)
        => new(2, arg0, arg1);

    public static LogArguments Create<T0, T1, T2>(T0 arg0, T1 arg1, T2 arg2)
        => new(3, arg0, arg1, arg2);

    public static LogArguments Create<T0, T1, T2, T3>(T0 arg0, T1 arg1, T2 arg2, T3 arg3)
        => new(4, arg0, arg1, arg2, arg3);

    public static LogArguments Create<T0, T1, T2, T3, T4>(T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        => new(5, arg0, arg1, arg2, arg3, arg4);

    public static LogArguments Create<T0, T1, T2, T3, T4, T5>(T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        => new(6, arg0, arg1, arg2, arg3, arg4, arg5);

    public static LogArguments FromArray(object?[]? args)
        => args is { Length: > 0 } ? new LogArguments(args.Length, array: args) : Empty;

    internal object?[] ToArray()
    {
        if (_array != null)
            return _array;

        var result = new object?[Count];
        for (var i = 0; i < Count; i++)
            result[i] = this[i];
        return result;
    }
}

public readonly struct LogRecord
{
    public required long Sequence { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required LogLevel Level { get; init; }
    public required string Name { get; init; }
    public required string MessageTemplate { get; init; }
    public required LogArguments Arguments { get; init; }
    public Exception? Exception { get; init; }
    public required int ThreadId { get; init; }
    public string? ThreadName { get; init; }
}

public readonly record struct LogStatistics(
    long Enqueued,
    long Processed,
    long Dropped,
    long Queued,
    int QueueCapacity);
