using NetStickShared.Net.API;

namespace NetStickShared.Packets;

[Packet(0, PacketType.kClient)]
public partial class CTestPacket : IPacket
{
    [PacketField(FieldType.kString)]
    public string Text { get; set; } = string.Empty;
    
    [PacketField(FieldType.kInt32)]
    public int Number { get; set; }

    public int PacketId => 0;
    public PacketType PacketType => PacketType.kClient;
}

[Packet(0, PacketType.kServer)]
public partial class STestPacket : IPacket
{
    [PacketField(FieldType.kString)]
    public string Text { get; set; } = string.Empty;
    
    [PacketField(FieldType.kBool)]
    public bool State { get; set; }

    public int PacketId => 0;
    public PacketType PacketType => PacketType.kServer;
}