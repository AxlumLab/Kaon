using Kaon.Protocol.Packet;

namespace Kaon.Packets;

[Packet(10, PacketType.kClient)]
public partial class CSessionHelloPacket : IPacket
{
    [PacketField(0, FieldType.kString)]
    public string ClientName { get; set; } = string.Empty;

    [PacketField(1, FieldType.kString)]
    public string ClientInstanceId { get; set; } = string.Empty;

    [PacketField(2, FieldType.kString)]
    public string Capabilities { get; set; } = string.Empty;
}

[Packet(10, PacketType.kServer)]
public partial class SSessionAcceptedPacket : IPacket
{
    [PacketField(0, FieldType.kString)]
    public string SessionId { get; set; } = string.Empty;

    [PacketField(1, FieldType.kString)]
    public string AttachToken { get; set; } = string.Empty;

    [PacketField(2, FieldType.kInt32)]
    public int TunnelId { get; set; }
}

[Packet(11, PacketType.kClient)]
public partial class CAttachTunnelPacket : IPacket
{
    [PacketField(0, FieldType.kString)]
    public string SessionId { get; set; } = string.Empty;

    [PacketField(1, FieldType.kString)]
    public string AttachToken { get; set; } = string.Empty;

    [PacketField(2, FieldType.kString)]
    public string ClientNonce { get; set; } = string.Empty;
}

[Packet(11, PacketType.kServer)]
public partial class STunnelAttachedPacket : IPacket
{
    [PacketField(0, FieldType.kBool)]
    public bool Success { get; set; }

    [PacketField(1, FieldType.kInt32)]
    public int TunnelId { get; set; }

    [PacketField(2, FieldType.kString, nullable: true)]
    public string? Message { get; set; }
}

[Packet(1, PacketType.kClient)]
public partial class CRegisterServicePacket : IPacket
{
    [PacketField(0, FieldType.kString)]
    public string ClientName { get; set; } = string.Empty;

    [PacketField(1, FieldType.kString)]
    public string ServiceName { get; set; } = string.Empty;

    [PacketField(2, FieldType.kString)]
    public string Protocol { get; set; } = string.Empty;

    [PacketField(3, FieldType.kString)]
    public string LocalEndPoint { get; set; } = string.Empty;

    [PacketField(4, FieldType.kString, nullable: true)]
    public string? PublicEndPoint { get; set; }
}

[Packet(1, PacketType.kServer)]
public partial class SRegisterServiceResultPacket : IPacket
{
    [PacketField(0, FieldType.kBool)]
    public bool Success { get; set; }

    [PacketField(1, FieldType.kString, nullable: true)]
    public string? Message { get; set; }
}

[Packet(2, PacketType.kServer)]
public partial class SOpenStreamPacket : IPacket
{
    [PacketField(0, FieldType.kString)]
    public string ServiceName { get; set; } = string.Empty;
}

[Packet(2, PacketType.kClient)]
public partial class COpenStreamPacket : IPacket
{
    [PacketField(0, FieldType.kString)]
    public string ServiceName { get; set; } = string.Empty;
}

[Packet(4, PacketType.kClient)]
public partial class COpenStreamResultPacket : IPacket
{
    [PacketField(0, FieldType.kBool)]
    public bool Success { get; set; }

    [PacketField(1, FieldType.kString, nullable: true)]
    public string? Message { get; set; }
}

[Packet(4, PacketType.kServer)]
public partial class SOpenStreamResultPacket : IPacket
{
    [PacketField(0, FieldType.kBool)]
    public bool Success { get; set; }

    [PacketField(1, FieldType.kString, nullable: true)]
    public string? Message { get; set; }
}

[Packet(3, PacketType.kClient)]
public partial class CUnregisterServicePacket : IPacket
{
    [PacketField(0, FieldType.kString)]
    public string ServiceName { get; set; } = string.Empty;
}

[Packet(3, PacketType.kServer)]
public partial class SServiceStatusPacket : IPacket
{
    [PacketField(0, FieldType.kString)]
    public string ServiceName { get; set; } = string.Empty;

    [PacketField(1, FieldType.kBool)]
    public bool Available { get; set; }

    [PacketField(2, FieldType.kString, nullable: true)]
    public string? Message { get; set; }
}

[Packet(5, PacketType.kClient)]
public partial class CListServicesPacket : IPacket
{
    [PacketField(0, FieldType.kString)]
    public string Filter { get; set; } = string.Empty;
}

[Packet(5, PacketType.kServer)]
public partial class SServiceListPacket : IPacket
{
    [PacketField(0, FieldType.kString)]
    public string Services { get; set; } = string.Empty;
}
