using System;
using System.Collections.Generic;
using NetStickShared.Logger;

namespace NetStickShared.Net.API;

public delegate IPacket PacketEmitter();

[AttributeUsage(AttributeTargets.Class)]
public class PacketMapperAttribute : Attribute;

public interface IPacketMapper
{
    void RegisterPackets(PacketRegistry reg);
}

public sealed class PacketRegistry
{
    public static PacketRegistry Create(IPacketMapper mapper)
    {
        if (_instance != null)
            return _instance;
        lock (_lock)
        {
            _instance = new PacketRegistry(mapper);
            return _instance;
        }
    }
    
    private PacketRegistry(IPacketMapper mapper)
    {
        mapper.RegisterPackets(this);
#if DEBUG
        _logger.Debug($"Registered packet server:{_serverPackets.Count} client:{_clientPackets.Count}");
#endif
    }

    public void RegisterClient<T>(int id)
        where T : IPacket, new()
    {
        _clientPackets[id] = static () => new T();
    }
    
    public void RegisterServer<T>(int id)
        where T : IPacket, new()
    {
        _serverPackets[id] = static () => new T();
    }

    public IPacket? CreatePacket(PacketType type, int id)
    {
        return type switch
        {
            PacketType.kServer => !_serverPackets.TryGetValue(id, out var packet1) ? null : packet1(),
            PacketType.kClient => !_clientPackets.TryGetValue(id, out var packet2) ? null : packet2(),
            _ => null
        };
    }
    
    public IPacket CreateClient(int id)
    {
        return _clientPackets[id]();
    }
    
    public IPacket CreateServer(int id)
    {
        return _serverPackets[id]();
    }
    
    private readonly Dictionary<int, PacketEmitter> _clientPackets = new();
    private readonly Dictionary<int, PacketEmitter> _serverPackets = new();
#if DEBUG
    private readonly ILogger _logger = LogManager.GetLogger(typeof(PacketRegistry));
#endif
    
    private static readonly object _lock = new();
    private static PacketRegistry? _instance;
}
