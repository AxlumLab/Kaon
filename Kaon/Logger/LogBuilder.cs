using System.Globalization;
using System.Text;

namespace Kaon.Logger;

internal sealed class LogFormatter(string pattern, string timestampFormat)
{
    private readonly Segment[] _segments = Compile(pattern);
    private readonly StringBuilder _builder = new(256);

    public string Format(in LogRecord record)
    {
        _builder.Clear();
        var message = FormatMessage(in record);

        foreach (var segment in _segments)
        {
            switch (segment.Token)
            {
                case LogToken.Literal:
                    _builder.Append(segment.Literal);
                    break;
                case LogToken.Time:
                    _builder.Append(record.Timestamp.ToLocalTime().ToString(timestampFormat, CultureInfo.InvariantCulture));
                    break;
                case LogToken.Name:
                    _builder.Append(record.Name);
                    break;
                case LogToken.Level:
                    _builder.Append(GetLevelName(record.Level));
                    break;
                case LogToken.ShortLevel:
                    _builder.Append(GetLevelName(record.Level)[0]);
                    break;
                case LogToken.Message:
                    _builder.Append(message);
                    break;
                case LogToken.Thread:
                    _builder.Append(record.ThreadName ?? string.Empty);
                    break;
                case LogToken.ThreadId:
                    _builder.Append(record.ThreadId);
                    break;
            }
        }

        if (record.Exception == null) 
            return _builder.ToString();
        
        if (_builder.Length > 0)
            _builder.AppendLine();
        _builder.Append(record.Exception);

        return _builder.ToString();
    }

    private static string FormatMessage(in LogRecord record)
    {
        try
        {
            return record.Arguments.Count switch
            {
                0 => record.MessageTemplate,
                1 => string.Format(CultureInfo.InvariantCulture, record.MessageTemplate, record.Arguments[0]),
                2 => string.Format(CultureInfo.InvariantCulture, record.MessageTemplate, record.Arguments[0], record.Arguments[1]),
                3 => string.Format(CultureInfo.InvariantCulture, record.MessageTemplate, record.Arguments[0], record.Arguments[1], record.Arguments[2]),
                _ => string.Format(CultureInfo.InvariantCulture, record.MessageTemplate, record.Arguments.ToArray())
            };
        }
        catch (FormatException)
        {
            var builder = new StringBuilder(record.MessageTemplate.Length + 64);
            builder.Append(record.MessageTemplate).Append(" [format-error args=");
            for (var i = 0; i < record.Arguments.Count; i++)
            {
                if (i != 0)
                    builder.Append(", ");
                builder.Append(record.Arguments[i]);
            }
            return builder.Append(']').ToString();
        }
    }

    private static Segment[] Compile(string pattern)
    {
        var result = new List<Segment>();
        var literalStart = 0;

        for (var i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] != '{' || i + 2 >= pattern.Length || pattern[i + 1] != '$')
                continue;

            var end = pattern.IndexOf('}', i + 2);
            if (end < 0)
                break;

            if (i > literalStart)
                result.Add(Segment.LiteralText(pattern[literalStart..i]));

            var token = pattern.AsSpan(i + 2, end - i - 2);
            result.Add(TokenSegment(token));
            i = end;
            literalStart = end + 1;
        }

        if (literalStart < pattern.Length)
            result.Add(Segment.LiteralText(pattern[literalStart..]));

        return result.ToArray();
    }

    private static Segment TokenSegment(ReadOnlySpan<char> token)
    {
        return token switch
        {
            "Time" => new Segment(LogToken.Time, null),
            "Name" => new Segment(LogToken.Name, null),
            "Level" => new Segment(LogToken.Level, null),
            "SLevel" => new Segment(LogToken.ShortLevel, null),
            "Message" => new Segment(LogToken.Message, null),
            "Thread" => new Segment(LogToken.Thread, null),
            "ThreadId" => new Segment(LogToken.ThreadId, null),
            "C" => new Segment(LogToken.Literal, null),
            _ => Segment.LiteralText("{$" + token.ToString() + "}")
        };
    }

    internal static string GetLevelName(LogLevel level)
        => level switch
        {
            LogLevel.kTrace => "Trace",
            LogLevel.kDebug => "Debug",
            LogLevel.kInfo => "Info",
            LogLevel.kWarn => "Warn",
            LogLevel.kError => "Error",
            LogLevel.kFatal => "Fatal",
            _ => level.ToString()
        };

    private readonly record struct Segment(LogToken Token, string? Literal)
    {
        public static Segment LiteralText(string text) => new(LogToken.Literal, text);
    }

    private enum LogToken
    {
        Literal,
        Time,
        Name,
        Level,
        ShortLevel,
        Message,
        Thread,
        ThreadId
    }
}
