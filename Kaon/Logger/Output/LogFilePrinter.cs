using System.Text;

namespace Kaon.Logger.Output;

public sealed class LogFilePrinter(string? directory = null, LogLevel minimumLevel = LogLevel.kTrace)
    : IOutput
{
    private StreamWriter? _writer;

    public LogLevel MinimumLevel { get; } = minimumLevel;
    public string? FilePath { get; private set; }

    public void Init(LogOptions options)
    {
        var directory1 = string.IsNullOrWhiteSpace(directory) ? options.LogDirectory : directory;
        Directory.CreateDirectory(directory1);

        var fileName = $"kaon_{DateTime.Now:yyyyMMdd_HHmmss}_{Environment.ProcessId}.log";
        FilePath = Path.Combine(directory1, fileName);
        var stream = new FileStream(
            FilePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.ReadWrite,
            options.FileBufferSize,
            FileOptions.SequentialScan);

        _writer = new StreamWriter(stream, new UTF8Encoding(false), options.FileBufferSize)
        {
            AutoFlush = false
        };
    }

    public void Output(in LogRecord record, string formattedMessage)
        => _writer?.WriteLine(formattedMessage);

    public void Flush()
        => _writer?.Flush();

    public void Stop()
    {
        var writer = Interlocked.Exchange(ref _writer, null);
        if (writer == null)
            return;

        writer.Flush();
        writer.Dispose();
    }
}
