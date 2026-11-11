using System.Globalization;

namespace Kaon.Config;

public interface IConfigValueSerializer<T>
{
    string Serialize(T value);
    T Deserialize(string value);
}

public sealed class ConfigSerializerRegistry : IDisposable
{
    private readonly Dictionary<Type, IConfigValueSerializerBox> _typeSerializers = new();
    private readonly Dictionary<string, IConfigValueSerializerBox> _nodeSerializers = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IDisposable> _disposables = [];

    public void Register<T>(IConfigValueSerializer<T> serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        TrackDisposable(serializer);
        _typeSerializers[typeof(T)] = new ConfigValueSerializerBox<T>(serializer);
    }

    public void Register<T>(string key, IConfigValueSerializer<T> serializer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(serializer);
        TrackDisposable(serializer);
        _nodeSerializers[NormalizeKey(key)] = new ConfigValueSerializerBox<T>(serializer);
    }

    public string Serialize<T>(string key, T value)
    {
        if (TryGetSerializer<T>(key, out var serializer))
            return serializer.Serialize(value);

        return SerializeDefault(value);
    }

    public bool TryDeserialize<T>(string key, string raw, out T value)
    {
        if (TryGetSerializer<T>(key, out var serializer))
        {
            value = serializer.Deserialize(raw);
            return true;
        }

        return TryDeserializeDefault(raw, out value);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
            disposable.Dispose();

        _disposables.Clear();
        _typeSerializers.Clear();
        _nodeSerializers.Clear();
    }

    private bool TryGetSerializer<T>(string key, out IConfigValueSerializerBox<T> serializer)
    {
        if (_nodeSerializers.TryGetValue(NormalizeKey(key), out var nodeSerializer) &&
            nodeSerializer is IConfigValueSerializerBox<T> typedNodeSerializer)
        {
            serializer = typedNodeSerializer;
            return true;
        }

        if (_typeSerializers.TryGetValue(typeof(T), out var typeSerializer) &&
            typeSerializer is IConfigValueSerializerBox<T> typedTypeSerializer)
        {
            serializer = typedTypeSerializer;
            return true;
        }

        serializer = null!;
        return false;
    }

    private static string SerializeDefault<T>(T value)
    {
        return value switch
        {
            null => "",
            string text => text,
            bool boolean => boolean ? "true" : "false",
            byte number => number.ToString(CultureInfo.InvariantCulture),
            sbyte number => number.ToString(CultureInfo.InvariantCulture),
            short number => number.ToString(CultureInfo.InvariantCulture),
            ushort number => number.ToString(CultureInfo.InvariantCulture),
            int number => number.ToString(CultureInfo.InvariantCulture),
            uint number => number.ToString(CultureInfo.InvariantCulture),
            long number => number.ToString(CultureInfo.InvariantCulture),
            ulong number => number.ToString(CultureInfo.InvariantCulture),
            float number => number.ToString(CultureInfo.InvariantCulture),
            double number => number.ToString(CultureInfo.InvariantCulture),
            decimal number => number.ToString(CultureInfo.InvariantCulture),
            char ch => ch.ToString(),
            _ => throw new NotSupportedException($"No config serializer registered for {typeof(T).FullName}.")
        };
    }

    private static bool TryDeserializeDefault<T>(string raw, out T value)
    {
        var type = typeof(T);

        try
        {
            object parsed;
            if (type == typeof(string))
                parsed = raw;
            else if (type == typeof(bool))
                parsed = ParseBool(raw);
            else if (type == typeof(byte))
                parsed = byte.Parse(raw, CultureInfo.InvariantCulture);
            else if (type == typeof(sbyte))
                parsed = sbyte.Parse(raw, CultureInfo.InvariantCulture);
            else if (type == typeof(short))
                parsed = short.Parse(raw, CultureInfo.InvariantCulture);
            else if (type == typeof(ushort))
                parsed = ushort.Parse(raw, CultureInfo.InvariantCulture);
            else if (type == typeof(int))
                parsed = int.Parse(raw, CultureInfo.InvariantCulture);
            else if (type == typeof(uint))
                parsed = uint.Parse(raw, CultureInfo.InvariantCulture);
            else if (type == typeof(long))
                parsed = long.Parse(raw, CultureInfo.InvariantCulture);
            else if (type == typeof(ulong))
                parsed = ulong.Parse(raw, CultureInfo.InvariantCulture);
            else if (type == typeof(float))
                parsed = float.Parse(raw, CultureInfo.InvariantCulture);
            else if (type == typeof(double))
                parsed = double.Parse(raw, CultureInfo.InvariantCulture);
            else if (type == typeof(decimal))
                parsed = decimal.Parse(raw, CultureInfo.InvariantCulture);
            else if (type == typeof(char))
                parsed = raw.Length > 0 ? raw[0] : throw new FormatException("Empty char value.");
            else
                throw new NotSupportedException($"No config serializer registered for {type.FullName}.");

            value = (T)parsed;
            return true;
        }
        catch
        {
            value = default!;
            return false;
        }
    }

    private static bool ParseBool(string raw)
    {
        return raw.Trim().ToLowerInvariant() switch
        {
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

    private static string NormalizeKey(string key)
        => Kaon.App.CmdLine.NormalizeKey(key);

    private void TrackDisposable<T>(IConfigValueSerializer<T> serializer)
    {
        if (serializer is IDisposable disposable)
            _disposables.Add(disposable);
    }

    private interface IConfigValueSerializerBox
    {
    }

    private interface IConfigValueSerializerBox<T> : IConfigValueSerializerBox
    {
        string Serialize(T value);
        T Deserialize(string value);
    }

    private sealed class ConfigValueSerializerBox<T>(IConfigValueSerializer<T> serializer) : IConfigValueSerializerBox<T>
    {
        public string Serialize(T value) => serializer.Serialize(value);
        public T Deserialize(string value) => serializer.Deserialize(value);
    }
}
