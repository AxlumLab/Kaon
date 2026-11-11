using System.Collections.ObjectModel;
using System.Globalization;

namespace Kaon.App;

public sealed class CmdLine
{
    private readonly string[] _args;
    private readonly Dictionary<string, Entry> _options = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _positionals = [];
    private readonly HashSet<int> _handled = [];

    private CmdLine(string[] args)
    {
        _args = args;
        Parse();
    }

    public static CmdLine Parse(IEnumerable<string>? args)
        => new((args ?? []).ToArray());

    public IReadOnlyList<string> Positionals => _positionals;

    public bool Contains(string key)
        => _options.ContainsKey(NormalizeKey(key));

    public bool TryGetValue(string key, out string value)
    {
        if (_options.TryGetValue(NormalizeKey(key), out var entry))
        {
            value = entry.Value;
            return true;
        }

        value = "";
        return false;
    }

    public bool TryTakeValue(string key, out string value)
    {
        if (!TryGetValue(key, out value))
            return false;

        var entry = _options[NormalizeKey(key)];
        foreach (var index in entry.SourceIndexes)
            _handled.Add(index);
        return true;
    }

    public bool TryTake<T>(string key, out T value)
    {
        if (TryTakeValue(key, out var raw) && TryConvert(raw, out value))
            return true;

        value = default!;
        return false;
    }

    public IReadOnlyList<string> GetUnhandledArgs()
    {
        var result = new List<string>();
        for (var i = 0; i < _args.Length; i++)
        {
            if (!_handled.Contains(i))
                result.Add(_args[i]);
        }

        return new ReadOnlyCollection<string>(result);
    }

    internal static string NormalizeKey(string key)
    {
        var span = key.AsSpan().Trim();
        while (span.Length > 0 && (span[0] == '-' || span[0] == '/'))
            span = span[1..];

        Span<char> buffer = stackalloc char[span.Length];
        var length = 0;
        foreach (var ch in span)
        {
            if (char.IsLetterOrDigit(ch))
                buffer[length++] = char.ToLowerInvariant(ch);
        }

        return new string(buffer[..length]);
    }

    internal static bool TryConvert<T>(string raw, out T value)
    {
        try
        {
            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            object converted;

            if (targetType == typeof(string))
                converted = raw;
            else if (targetType == typeof(bool))
                converted = ParseBool(raw);
            else if (targetType.IsEnum)
                converted = Enum.Parse(targetType, raw, ignoreCase: true);
            else
                converted = Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);

            value = (T)converted;
            return true;
        }
        catch
        {
            value = default!;
            return false;
        }
    }

    private void Parse()
    {
        for (var i = 0; i < _args.Length; i++)
        {
            var arg = _args[i];
            if (!LooksLikeOption(arg))
            {
                _positionals.Add(arg);
                continue;
            }

            var token = arg.TrimStart('-', '/');
            var separator = token.IndexOfAny(['=', ':']);
            var key = separator >= 0 ? token[..separator] : token;
            var value = separator >= 0 ? token[(separator + 1)..] : "true";
            var sourceIndexes = new List<int> { i };

            if (separator < 0 && i + 1 < _args.Length && !LooksLikeOption(_args[i + 1]))
            {
                value = _args[++i];
                sourceIndexes.Add(i);
            }

            AddOption(key, value, sourceIndexes);
        }
    }

    private void AddOption(string key, string value, List<int> sourceIndexes)
    {
        var normalized = NormalizeKey(key);
        if (normalized.Length == 0)
            return;

        _options[normalized] = new Entry(value, sourceIndexes);
    }

    private static bool LooksLikeOption(string arg)
        => arg.Length > 1 && (arg[0] == '-' || arg[0] == '/');

    private static bool ParseBool(string raw)
    {
        return raw.Trim().ToLowerInvariant() switch
        {
            "" => true,
            "1" => true,
            "true" => true,
            "yes" => true,
            "y" => true,
            "on" => true,
            "0" => false,
            "false" => false,
            "no" => false,
            "n" => false,
            "off" => false,
            _ => bool.Parse(raw)
        };
    }

    private sealed record Entry(string Value, IReadOnlyList<int> SourceIndexes);
}
