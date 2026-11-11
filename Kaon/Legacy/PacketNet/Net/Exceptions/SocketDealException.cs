namespace NetStickShared.Net.Exceptions;

public sealed class SocketDealException(ServerStatus status, Exception e) : Exception
{
    public new Exception InnerException { get; } = e;
    public string ErrorSocketName { get; } = "Form Server Util";
    public ServerStatus ServerNowStatus { get; } = status;
    public DateTime Time { get; } = DateTime.Now;
}