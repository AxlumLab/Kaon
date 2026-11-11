using System.Net;

namespace Kaon.Proxy;

/// <summary>
/// 代理通讯协议
/// </summary>
public enum ProxyProtocol
{
    Tcp,
    Udp,
}

/// <summary>
/// 表示客户端可以提供的一个本地服务
/// </summary>
public sealed record ProxyServiceDescriptor
{
    public string ClientName { get; init; } = string.Empty;

    public required string ServiceName { get; init; }

    public required ProxyProtocol Protocol { get; init; }

    public required EndPoint LocalEndPoint { get; init; }

    public EndPoint? PublicEndPoint { get; init; }
}
