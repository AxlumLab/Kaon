namespace Kaon.Protocol.Packet;

/// <summary>
/// Packet 控制消息语义标志。
///
/// 这些标志只用于描述请求、响应、通知和错误等业务语义，
/// 不应包含压缩、加密、校验等传输层状态。
/// </summary>
[Flags]
public enum PacketFlags : ushort
{
    /// <summary>
    /// 没有特殊语义。
    /// </summary>
    None = 0,

    /// <summary>
    /// 当前 Packet 是一个请求。
    ///
    /// 通常要求 RequestId 非零，并期待一个具有相同 RequestId 的响应。
    /// </summary>
    Request = 1 << 0,

    /// <summary>
    /// 当前 Packet 是某个请求的响应。
    ///
    /// Response 的 RequestId 必须与原请求一致。
    /// </summary>
    Response = 1 << 1,

    /// <summary>
    /// 当前 Packet 是单向通知。
    ///
    /// 发送方不等待业务响应，RequestId 通常为 0。
    /// </summary>
    OneWay = 1 << 2,

    /// <summary>
    /// 当前响应表示业务处理失败。
    ///
    /// 通常与 Response 一起使用：
    /// Response | Error。
    ///
    /// 具体错误码和错误信息应放在 Packet Body 中。
    /// </summary>
    Error = 1 << 3,

    /// <summary>
    /// 发送方要求接收方确认已经收到该 Packet。
    ///
    /// Ack 仅表示消息已收到，不代表业务执行成功。
    /// 第一版通常不需要实现。
    /// </summary>
    AckRequired = 1 << 4,

    /// <summary>
    /// 当前 Packet 是一个接收确认。
    ///
    /// 它只表示目标 Packet 已被接收，不等同于业务 Response。
    /// </summary>
    Ack = 1 << 5,

    /// <summary>
    /// 当前请求可以安全重复执行。
    ///
    /// 断线重试或超时重发时，服务端可根据 RequestId 去重。
    /// 是否真正幂等仍由具体 Packet Handler 保证。
    /// </summary>
    Idempotent = 1 << 6,

    /// <summary>
    /// 当前 Packet 具有较高处理优先级。
    ///
    /// 可用于关闭、认证失效、连接控制等消息。
    /// 第一版可以定义但暂不实现优先队列。
    /// </summary>
    HighPriority = 1 << 7,

    /// <summary>
    /// 当前 Packet 不应被自动重试。
    ///
    /// 适用于一次性操作或具有副作用的请求。
    /// </summary>
    NoRetry = 1 << 8,

    /// <summary>
    /// 保留位，供未来协议扩展。
    /// </summary>
    Reserved9 = 1 << 9,

    /// <summary>
    /// 保留位，供未来协议扩展。
    /// </summary>
    Reserved10 = 1 << 10,

    /// <summary>
    /// 保留位，供未来协议扩展。
    /// </summary>
    Reserved11 = 1 << 11,
}