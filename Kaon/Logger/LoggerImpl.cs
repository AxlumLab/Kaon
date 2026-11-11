using Kaon.Logger.Output;

namespace Kaon.Logger;

public sealed class LoggerImpl : ILogger
{
    private readonly LogOutput _output;

    internal LoggerImpl(string name)
    {
        Name = name;
        _output = LogManager.EnsureOutput();
    }

    public LoggerImpl(string name, params IOutput[] services)
    {
        Name = name;
        _output = LogOutput.Get() ?? LogOutput.New(LogManager.Options, services);
    }

    public string Name { get; }

    public bool IsEnabled(LogLevel level)
        => _output.IsEnabled(level, Name);

    public void Write(LogLevel level, string message, Exception? exception = null)
    {
        var arguments = LogArguments.Empty;
        _output.Write(level, Name, message, in arguments, exception);
    }

    public void Write(LogLevel level, string template, in LogArguments arguments, Exception? exception = null)
        => _output.Write(level, Name, template, in arguments, exception);
}
