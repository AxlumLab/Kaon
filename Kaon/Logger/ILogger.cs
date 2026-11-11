namespace Kaon.Logger;
public interface ILogger
{
    string Name { get; }
    bool IsEnabled(LogLevel level);
    void Write(LogLevel level, string message, Exception? exception = null);
    void Write(LogLevel level, string template, in LogArguments arguments, Exception? exception = null);
}

public static class LoggerExtensions
{
    extension(ILogger logger)
    {
        public void Trace(string message)
            => WriteMessage(logger, LogLevel.kTrace, message, null);

        public void Trace<T0>(string template, T0 arg0)
            => WriteTemplate(logger, LogLevel.kTrace, template, LogArguments.Create(arg0), null);

        public void Trace<T0, T1>(string template, T0 arg0, T1 arg1)
            => WriteTemplate(logger, LogLevel.kTrace, template, LogArguments.Create(arg0, arg1), null);

        public void Trace<T0, T1, T2>(string template, T0 arg0, T1 arg1, T2 arg2)
            => WriteTemplate(logger, LogLevel.kTrace, template, LogArguments.Create(arg0, arg1, arg2), null);

        public void Trace<T0, T1, T2, T3>(string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
            => WriteTemplate(logger, LogLevel.kTrace, template, LogArguments.Create(arg0, arg1, arg2, arg3), null);

        public void Trace<T0, T1, T2, T3, T4>(string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            => WriteTemplate(logger, LogLevel.kTrace, template, LogArguments.Create(arg0, arg1, arg2, arg3, arg4), null);

        public void Trace<T0, T1, T2, T3, T4, T5>(string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            => WriteTemplate(logger, LogLevel.kTrace, template, LogArguments.Create(arg0, arg1, arg2, arg3, arg4, arg5), null);

        public void Trace(string template, params object?[] args)
            => WriteTemplate(logger, LogLevel.kTrace, template, LogArguments.FromArray(args), null);

        public void Debug(string message)
            => WriteMessage(logger, LogLevel.kDebug, message, null);

        public void Debug<T0>(string template, T0 arg0)
            => WriteTemplate(logger, LogLevel.kDebug, template, LogArguments.Create(arg0), null);

        public void Debug<T0, T1>(string template, T0 arg0, T1 arg1)
            => WriteTemplate(logger, LogLevel.kDebug, template, LogArguments.Create(arg0, arg1), null);

        public void Debug<T0, T1, T2>(string template, T0 arg0, T1 arg1, T2 arg2)
            => WriteTemplate(logger, LogLevel.kDebug, template, LogArguments.Create(arg0, arg1, arg2), null);

        public void Debug<T0, T1, T2, T3>(string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
            => WriteTemplate(logger, LogLevel.kDebug, template, LogArguments.Create(arg0, arg1, arg2, arg3), null);

        public void Debug<T0, T1, T2, T3, T4>(string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            => WriteTemplate(logger, LogLevel.kDebug, template, LogArguments.Create(arg0, arg1, arg2, arg3, arg4), null);

        public void Debug<T0, T1, T2, T3, T4, T5>(string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            => WriteTemplate(logger, LogLevel.kDebug, template, LogArguments.Create(arg0, arg1, arg2, arg3, arg4, arg5), null);

        public void Debug(string template, params object?[] args)
            => WriteTemplate(logger, LogLevel.kDebug, template, LogArguments.FromArray(args), null);

        public void Info(string message)
            => WriteMessage(logger, LogLevel.kInfo, message, null);

        public void Info<T0>(string template, T0 arg0)
            => WriteTemplate(logger, LogLevel.kInfo, template, LogArguments.Create(arg0), null);

        public void Info<T0, T1>(string template, T0 arg0, T1 arg1)
            => WriteTemplate(logger, LogLevel.kInfo, template, LogArguments.Create(arg0, arg1), null);

        public void Info<T0, T1, T2>(string template, T0 arg0, T1 arg1, T2 arg2)
            => WriteTemplate(logger, LogLevel.kInfo, template, LogArguments.Create(arg0, arg1, arg2), null);

        public void Info<T0, T1, T2, T3>(string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
            => WriteTemplate(logger, LogLevel.kInfo, template, LogArguments.Create(arg0, arg1, arg2, arg3), null);

        public void Info<T0, T1, T2, T3, T4>(string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            => WriteTemplate(logger, LogLevel.kInfo, template, LogArguments.Create(arg0, arg1, arg2, arg3, arg4), null);

        public void Info<T0, T1, T2, T3, T4, T5>(string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            => WriteTemplate(logger, LogLevel.kInfo, template, LogArguments.Create(arg0, arg1, arg2, arg3, arg4, arg5), null);

        public void Info(string template, params object?[] args)
            => WriteTemplate(logger, LogLevel.kInfo, template, LogArguments.FromArray(args), null);

        public void Warn(string message)
            => WriteMessage(logger, LogLevel.kWarn, message, null);

        public void Warn<T0>(string template, T0 arg0)
            => WriteTemplate(logger, LogLevel.kWarn, template, LogArguments.Create(arg0), null);

        public void Warn<T0, T1>(string template, T0 arg0, T1 arg1)
            => WriteTemplate(logger, LogLevel.kWarn, template, LogArguments.Create(arg0, arg1), null);

        public void Warn<T0, T1, T2>(string template, T0 arg0, T1 arg1, T2 arg2)
            => WriteTemplate(logger, LogLevel.kWarn, template, LogArguments.Create(arg0, arg1, arg2), null);

        public void Warn<T0, T1, T2, T3>(string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
            => WriteTemplate(logger, LogLevel.kWarn, template, LogArguments.Create(arg0, arg1, arg2, arg3), null);

        public void Warn<T0, T1, T2, T3, T4>(string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            => WriteTemplate(logger, LogLevel.kWarn, template, LogArguments.Create(arg0, arg1, arg2, arg3, arg4), null);

        public void Warn<T0, T1, T2, T3, T4, T5>(string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            => WriteTemplate(logger, LogLevel.kWarn, template, LogArguments.Create(arg0, arg1, arg2, arg3, arg4, arg5), null);

        public void Warn(string template, params object?[] args)
            => WriteTemplate(logger, LogLevel.kWarn, template, LogArguments.FromArray(args), null);

        public void Error(string message)
            => WriteMessage(logger, LogLevel.kError, message, null);

        public void Error<T0>(string template, T0 arg0)
            => WriteTemplate(logger, LogLevel.kError, template, LogArguments.Create(arg0), null);

        public void Error<T0, T1>(string template, T0 arg0, T1 arg1)
            => WriteTemplate(logger, LogLevel.kError, template, LogArguments.Create(arg0, arg1), null);

        public void Error<T0, T1, T2>(string template, T0 arg0, T1 arg1, T2 arg2)
            => WriteTemplate(logger, LogLevel.kError, template, LogArguments.Create(arg0, arg1, arg2), null);

        public void Error<T0, T1, T2, T3>(string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
            => WriteTemplate(logger, LogLevel.kError, template, LogArguments.Create(arg0, arg1, arg2, arg3), null);

        public void Error<T0, T1, T2, T3, T4>(string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            => WriteTemplate(logger, LogLevel.kError, template, LogArguments.Create(arg0, arg1, arg2, arg3, arg4), null);

        public void Error<T0, T1, T2, T3, T4, T5>(string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            => WriteTemplate(logger, LogLevel.kError, template, LogArguments.Create(arg0, arg1, arg2, arg3, arg4, arg5), null);

        public void Error(string template, params object?[] args)
            => WriteTemplate(logger, LogLevel.kError, template, LogArguments.FromArray(args), null);

        public void Fatal(string message)
            => WriteMessage(logger, LogLevel.kFatal, message, null);

        public void Fatal<T0>(string template, T0 arg0)
            => WriteTemplate(logger, LogLevel.kFatal, template, LogArguments.Create(arg0), null);

        public void Fatal<T0, T1>(string template, T0 arg0, T1 arg1)
            => WriteTemplate(logger, LogLevel.kFatal, template, LogArguments.Create(arg0, arg1), null);

        public void Fatal<T0, T1, T2>(string template, T0 arg0, T1 arg1, T2 arg2)
            => WriteTemplate(logger, LogLevel.kFatal, template, LogArguments.Create(arg0, arg1, arg2), null);

        public void Fatal<T0, T1, T2, T3>(string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
            => WriteTemplate(logger, LogLevel.kFatal, template, LogArguments.Create(arg0, arg1, arg2, arg3), null);

        public void Fatal<T0, T1, T2, T3, T4>(string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            => WriteTemplate(logger, LogLevel.kFatal, template, LogArguments.Create(arg0, arg1, arg2, arg3, arg4), null);

        public void Fatal<T0, T1, T2, T3, T4, T5>(string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            => WriteTemplate(logger, LogLevel.kFatal, template, LogArguments.Create(arg0, arg1, arg2, arg3, arg4, arg5), null);

        public void Fatal(string template, params object?[] args)
            => WriteTemplate(logger, LogLevel.kFatal, template, LogArguments.FromArray(args), null);

        public void Warn(Exception exception, string message = "")
            => WriteMessage(logger, LogLevel.kWarn, message, exception);

        public void Warn<T0>(Exception exception, string template, T0 arg0)
            => WriteTemplate(logger, LogLevel.kWarn, template, LogArguments.Create(arg0), exception);

        public void Warn<T0, T1>(Exception exception, string template, T0 arg0, T1 arg1)
            => WriteTemplate(logger, LogLevel.kWarn, template, LogArguments.Create(arg0, arg1), exception);

        public void Warn<T0, T1, T2>(Exception exception, string template, T0 arg0, T1 arg1, T2 arg2)
            => WriteTemplate(logger, LogLevel.kWarn, template, LogArguments.Create(arg0, arg1, arg2), exception);

        public void Warn<T0, T1, T2, T3>(Exception exception, string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
            => WriteTemplate(logger, LogLevel.kWarn, template, LogArguments.Create(arg0, arg1, arg2, arg3), exception);

        public void Warn<T0, T1, T2, T3, T4>(Exception exception, string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            => WriteTemplate(logger, LogLevel.kWarn, template, LogArguments.Create(arg0, arg1, arg2, arg3, arg4), exception);

        public void Warn<T0, T1, T2, T3, T4, T5>(Exception exception, string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            => WriteTemplate(logger, LogLevel.kWarn, template, LogArguments.Create(arg0, arg1, arg2, arg3, arg4, arg5), exception);

        public void Warn(Exception exception, string template, params object?[] args)
            => WriteTemplate(logger, LogLevel.kWarn, template, LogArguments.FromArray(args), exception);

        public void Error(Exception exception, string message = "")
            => WriteMessage(logger, LogLevel.kError, message, exception);

        public void Error<T0>(Exception exception, string template, T0 arg0)
            => WriteTemplate(logger, LogLevel.kError, template, LogArguments.Create(arg0), exception);

        public void Error<T0, T1>(Exception exception, string template, T0 arg0, T1 arg1)
            => WriteTemplate(logger, LogLevel.kError, template, LogArguments.Create(arg0, arg1), exception);

        public void Error<T0, T1, T2>(Exception exception, string template, T0 arg0, T1 arg1, T2 arg2)
            => WriteTemplate(logger, LogLevel.kError, template, LogArguments.Create(arg0, arg1, arg2), exception);

        public void Error<T0, T1, T2, T3>(Exception exception, string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
            => WriteTemplate(logger, LogLevel.kError, template, LogArguments.Create(arg0, arg1, arg2, arg3), exception);

        public void Error<T0, T1, T2, T3, T4>(Exception exception, string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            => WriteTemplate(logger, LogLevel.kError, template, LogArguments.Create(arg0, arg1, arg2, arg3, arg4), exception);

        public void Error<T0, T1, T2, T3, T4, T5>(Exception exception, string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            => WriteTemplate(logger, LogLevel.kError, template, LogArguments.Create(arg0, arg1, arg2, arg3, arg4, arg5), exception);

        public void Error(Exception exception, string template, params object?[] args)
            => WriteTemplate(logger, LogLevel.kError, template, LogArguments.FromArray(args), exception);

        public void Fatal(Exception exception, string message = "")
            => WriteMessage(logger, LogLevel.kFatal, message, exception);

        public void Fatal<T0>(Exception exception, string template, T0 arg0)
            => WriteTemplate(logger, LogLevel.kFatal, template, LogArguments.Create(arg0), exception);

        public void Fatal<T0, T1>(Exception exception, string template, T0 arg0, T1 arg1)
            => WriteTemplate(logger, LogLevel.kFatal, template, LogArguments.Create(arg0, arg1), exception);

        public void Fatal<T0, T1, T2>(Exception exception, string template, T0 arg0, T1 arg1, T2 arg2)
            => WriteTemplate(logger, LogLevel.kFatal, template, LogArguments.Create(arg0, arg1, arg2), exception);

        public void Fatal<T0, T1, T2, T3>(Exception exception, string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
            => WriteTemplate(logger, LogLevel.kFatal, template, LogArguments.Create(arg0, arg1, arg2, arg3), exception);

        public void Fatal<T0, T1, T2, T3, T4>(Exception exception, string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            => WriteTemplate(logger, LogLevel.kFatal, template, LogArguments.Create(arg0, arg1, arg2, arg3, arg4), exception);

        public void Fatal<T0, T1, T2, T3, T4, T5>(Exception exception, string template, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            => WriteTemplate(logger, LogLevel.kFatal, template, LogArguments.Create(arg0, arg1, arg2, arg3, arg4, arg5), exception);

        public void Fatal(Exception exception, string template, params object?[] args)
            => WriteTemplate(logger, LogLevel.kFatal, template, LogArguments.FromArray(args), exception);
    }

    private static void WriteMessage(ILogger logger, LogLevel level, string message, Exception? exception)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (logger.IsEnabled(level))
            logger.Write(level, message, exception);
    }

    private static void WriteTemplate(ILogger logger, LogLevel level, string template, in LogArguments arguments, Exception? exception)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (logger.IsEnabled(level))
            logger.Write(level, template, in arguments, exception);
    }
}
