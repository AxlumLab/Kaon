namespace Kaon.Config;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ConfigAttribute : Attribute
{
    public ConfigAttribute()
    {
    }

    public ConfigAttribute(int version)
    {
        Version = version;
    }

    public ConfigAttribute(string fileName, int version = 1)
    {
        FileName = fileName;
        Version = version;
    }

    public ConfigAttribute(Type backendType, int version = 1)
    {
        BackendType = backendType;
        Version = version;
    }

    public ConfigAttribute(string fileName, Type backendType, int version = 1)
    {
        FileName = fileName;
        BackendType = backendType;
        Version = version;
    }

    public string? FileName { get; }
    public Type? BackendType { get; }
    public int Version { get; } = 1;
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ConfigNodeAttribute : Attribute
{
    public ConfigNodeAttribute()
    {
    }

    public ConfigNodeAttribute(string key)
    {
        Key = key;
    }

    public ConfigNodeAttribute(Type serializerType)
    {
        SerializerType = serializerType;
    }

    public ConfigNodeAttribute(string key, Type serializerType)
    {
        Key = key;
        SerializerType = serializerType;
    }

    public string? Key { get; }
    public Type? SerializerType { get; }
}
