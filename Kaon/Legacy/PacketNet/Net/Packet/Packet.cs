namespace NetStickShared.Net.API;

public enum PacketType : byte
{
    kNone,
    kServer,
    kClient
}

[AttributeUsage(AttributeTargets.Class)]
public class PacketAttribute(int id, PacketType type, string name = "") : Attribute
{
    public int PacketId { get; } = id;
    public PacketType PacketType { get; } = type;
    public string PacketName { get; } = name;
}

public enum FieldType : byte
{
    kUnknown = 0,
    kString,
    kBool,
    kInt32,
    kInt64,
    kByte,
    kSByte,
    kFloat,
    kDouble,
    kShort,
    kUShort,
    kVarInt,
    kVarLong,
    kComponent
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class PacketFieldAttribute(FieldType type, bool nullable = false) : Attribute
{
    public FieldType Type { get; } = type;
    public bool Nullable { get; } = nullable;
}