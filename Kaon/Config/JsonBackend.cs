using System.Globalization;
using Kaon.Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kaon.Config;

public class JsonBackend : IConfigBackend
{
    public Dictionary<string, string> ReadAll(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
            return values;

        try
        {
            var text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text))
                return values;

            var root = JObject.Parse(text);
            ReadObject(values, "", root);
        }
        catch (Exception e)
        {
            LogManager.GetSharedLogger().Error(e, "JsonBackend failed to read config {0}", path);
        }

        return values;
    }

    public bool WriteAll(string path, Dictionary<string, string> items)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var root = new JObject();
            foreach (var (key, value) in items)
                WriteValue(root, key, value);

            File.WriteAllText(path, root.ToString(Formatting.Indented));
            return File.Exists(path);
        }
        catch (Exception e)
        {
            LogManager.GetSharedLogger().Error(e, "JsonBackend failed to save config {0}", path);
            return false;
        }
    }

    private static void ReadObject(IDictionary<string, string> values, string prefix, JObject obj)
    {
        foreach (var (key, token) in obj)
        {
            var fullKey = string.IsNullOrEmpty(prefix) ? key : $"{prefix}.{key}";
            switch (token)
            {
                case JObject child:
                    ReadObject(values, fullKey, child);
                    break;
                case JValue value:
                    values[fullKey] = ConvertValue(value);
                    break;
            }
        }
    }

    private static void WriteValue(JObject root, string key, string value)
    {
        var parts = key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return;

        var current = root;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (current[parts[i]] is not JObject child)
            {
                child = new JObject();
                current[parts[i]] = child;
            }

            current = child;
        }

        current[parts[^1]] = ConvertToJsonValue(value);
    }

    private static string ConvertValue(JValue value)
    {
        return value.Value switch
        {
            null => "",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            var other => other.ToString() ?? ""
        };
    }

    private static JValue ConvertToJsonValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return JValue.CreateString(value);

        if (bool.TryParse(value, out var boolValue))
            return new JValue(boolValue);

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
            return new JValue(longValue);

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue) &&
            !double.IsNaN(doubleValue) &&
            !double.IsInfinity(doubleValue))
            return new JValue(doubleValue);

        return JValue.CreateString(value);
    }
}
