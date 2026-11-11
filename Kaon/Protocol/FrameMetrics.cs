namespace Kaon.Protocol;

public static class FrameMetrics
{
    private static long s_payloadCopiedBytes;
    private static long s_poolRentedBytes;
    private static long s_framesPassedThrough;
    private static long s_framesReplaced;
    private static long s_transformInputBytes;
    private static long s_transformOutputBytes;

    public static void AddPayloadCopy(int bytes) => Interlocked.Add(ref s_payloadCopiedBytes, Math.Max(0, bytes));

    public static void AddPoolRent(int bytes) => Interlocked.Add(ref s_poolRentedBytes, Math.Max(0, bytes));

    public static void AddTransformPassThrough(int bytes)
    {
        Interlocked.Increment(ref s_framesPassedThrough);
        Interlocked.Add(ref s_transformInputBytes, Math.Max(0, bytes));
    }

    public static void AddTransformReplacement(int inputBytes, int outputBytes)
    {
        Interlocked.Increment(ref s_framesReplaced);
        Interlocked.Add(ref s_transformInputBytes, Math.Max(0, inputBytes));
        Interlocked.Add(ref s_transformOutputBytes, Math.Max(0, outputBytes));
    }

    public static FrameMetricSnapshot Snapshot()
    {
        return new FrameMetricSnapshot(
            Volatile.Read(ref s_payloadCopiedBytes),
            Volatile.Read(ref s_poolRentedBytes),
            Volatile.Read(ref s_framesPassedThrough),
            Volatile.Read(ref s_framesReplaced),
            Volatile.Read(ref s_transformInputBytes),
            Volatile.Read(ref s_transformOutputBytes));
    }
}

public readonly record struct FrameMetricSnapshot(
    long PayloadCopiedBytes,
    long PoolRentedBytes,
    long FramesPassedThrough,
    long FramesReplaced,
    long TransformInputBytes,
    long TransformOutputBytes);
