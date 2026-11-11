namespace Kaon.Logger;

public static class ExceptionBuilder
{
    public static string Build(this Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.ToString();
    }
}
