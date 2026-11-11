namespace NetStickShared.Net.Exceptions;

public sealed class ServerUnknownAction : Exception
{
    public ServerUnknownAction(ServerAction action, string message, DateTime? time = null)
    {
        Message = message;
        Time = time ?? DateTime.Now;
        Action = action;
        GC.Collect();
    }
            
    public override string Message { get; }
    public ServerAction Action { get; }
    public DateTime Time { get; }
}

public enum ServerAction
{
    PRECONNECT = 0,
    CONNECT    = 1,
    OTHER      = -1
}