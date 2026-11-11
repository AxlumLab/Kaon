using Kaon.Logger.Output;

namespace Kaon.Logger;

public readonly struct LogItem
{
    public string Name { get; init; }
    public string Message { get; init; }
    public DateTime Time { get; init; }
    public LogLevel Level { get; init; }
    public string? ThreadName { get; init; }
    public int ThreadId { get; init; }
}

public sealed class LogBucket
{
    public void Enqueue(LogItem item)
        => LogOutput.Get()?.Write(item.Level, item.Name, item.Message);

    public void Stop()
    {
    }
}
