namespace Kaon.Logger;

public sealed class LogOptions
{
#if DEBUG
    public LogLevel MinimumLevel { get; set; } = LogLevel.kDebug;
#else
    public LogLevel MinimumLevel { get; set; } = LogLevel.kInfo;
#endif

    public int QueueCapacity { get; set; } = 32 * 1024;
    public int BatchSize { get; set; } = 256;
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public bool CaptureThreadName { get; set; }
    public bool EnableConsole { get; set; } = true;
    public bool EnableFile { get; set; } = true;
    public string LogDirectory { get; set; } = "logs";
    public int FileBufferSize { get; set; } = 64 * 1024;
    public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss.fff";
    public string Pattern { get; set; } = "[{$Time}] [{$SLevel}] {$Name}: {$Message}";

    internal void Validate()
    {
        QueueCapacity = Math.Max(256, QueueCapacity);
        BatchSize = Math.Clamp(BatchSize, 1, QueueCapacity);
        FileBufferSize = Math.Max(4096, FileBufferSize);
        if (FlushInterval <= TimeSpan.Zero)
            FlushInterval = TimeSpan.FromMilliseconds(250);
        if (ShutdownTimeout <= TimeSpan.Zero)
            ShutdownTimeout = TimeSpan.FromSeconds(5);
        if (string.IsNullOrWhiteSpace(TimestampFormat))
            TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";
        if (string.IsNullOrWhiteSpace(Pattern))
            Pattern = "[{$Time}] [{$SLevel}] {$Name}: {$Message}";
    }
}
