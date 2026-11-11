namespace Kaon.Protocol.Packet;

public enum PacketType : byte
{
    kNone,
    kServer,
    kClient
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

[AttributeUsage(AttributeTargets.Class)]
public class PacketAttribute(int id, PacketType type, string name = "", byte version = 1) : Attribute
{
    public int PacketId { get; } = id;
    public PacketType PacketType { get; } = type;
    public string PacketName { get; } = name;
    public byte PacketVersion { get; } = version;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class PacketFieldAttribute : Attribute
{
    public PacketFieldAttribute(FieldType type, bool nullable = false)
    {
        Type = type;
        Nullable = nullable;
        Order = -1;
    }

    public PacketFieldAttribute(int order, FieldType type, bool nullable = false)
    {
        Order = order;
        Type = type;
        Nullable = nullable;
    }

    public int Order { get; }
    public FieldType Type { get; }
    public bool Nullable { get; }
}
