using Kaon.Logger;

namespace Kaon.Logger.Output;

public sealed class LogPrinter(LogLevel minimumLevel = LogLevel.kTrace) : IOutput
{
    public LogLevel MinimumLevel { get; } = minimumLevel;

    public void Init(LogOptions options)
    {
    }

    public void Output(in LogRecord record, string formattedMessage)
    {
        if (Console.IsOutputRedirected)
        {
            Console.Out.WriteLine(formattedMessage);
            return;
        }

        var previous = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = GetColor(record.Level);
            Console.Out.WriteLine(formattedMessage);
        }
        finally
        {
            Console.ForegroundColor = previous;
        }
    }

    public void Flush()
        => Console.Out.Flush();

    public void Stop()
        => Flush();

    private static ConsoleColor GetColor(LogLevel level)
        => level switch
        {
            LogLevel.kTrace => ConsoleColor.DarkGray,
            LogLevel.kDebug => ConsoleColor.DarkCyan,
            LogLevel.kInfo => ConsoleColor.Gray,
            LogLevel.kWarn => ConsoleColor.Yellow,
            LogLevel.kError => ConsoleColor.Red,
            LogLevel.kFatal => ConsoleColor.DarkRed,
            _ => Console.ForegroundColor
        };
}
