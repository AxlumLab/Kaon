using System.Collections.Concurrent;
using System.Threading.Channels;
using Kaon.Logger.Output;

namespace Kaon.Logger;

public interface IOutput
{
    LogLevel MinimumLevel { get; }
    void Init(LogOptions options);
    void Output(in LogRecord record, string formattedMessage);
    void Flush();
    void Stop();
}

public sealed class LogOutput
{
    private static readonly Lock sLock = new();
    private static LogOutput? sOutput;
    private static readonly Lock sEmergencyLock = new();

    public static LogOutput New(params IOutput[] services)
        => New(LogManager.Options, services);

    internal static LogOutput New(LogOptions options, params IOutput[] services)
    {
        lock (sLock)
            return sOutput ??= new LogOutput(options, services);
    }

    public static LogOutput? Get() => Volatile.Read(ref sOutput);

    private readonly LogOptions _options;
    private readonly IOutput[] _backends;
    private readonly bool[] _backendEnabled;
    private readonly Channel<LogRecord> _queue;
    private readonly Thread _worker;
    private readonly ConcurrentDictionary<string, LogLevel> _categoryLevels = new(StringComparer.Ordinal);
    private LogFormatter _formatter;
    private long _sequence;
    private long _enqueued;
    private long _processed;
    private long _dropped;
    private long _droppedTotal;
    private int _minimumLevel;
    private int _stopped;

    private LogOutput(LogOptions options, IOutput[] services)
    {
        _options = options;
        _options.Validate();
        _minimumLevel = (int)_options.MinimumLevel;
        _backends = services.Length == 0 ? BuildDefaultOutputs(options) : services;
        _backendEnabled = new bool[_backends.Length];
        _formatter = new LogFormatter(options.Pattern, options.TimestampFormat);

        _queue = Channel.CreateBounded<LogRecord>(new BoundedChannelOptions(options.QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        for (var i = 0; i < _backends.Length; i++)
        {
            try
            {
                _backends[i].Init(options);
                _backendEnabled[i] = true;
            }
            catch (Exception e)
            {
                EmergencyWrite($"Kaon logger output init failed: {_backends[i].GetType().Name}: {e}");
            }
        }

        _worker = new Thread(Run)
        {
            IsBackground = true,
            Name = "Kaon.LogWriter",
            Priority = ThreadPriority.BelowNormal
        };
        _worker.Start();

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    public bool IsEnabled(LogLevel level, string category)
    {
        if (Volatile.Read(ref _stopped) != 0)
            return false;

        var minimum = _categoryLevels.TryGetValue(category, out var categoryLevel)
            ? categoryLevel
            : (LogLevel)Volatile.Read(ref _minimumLevel);

        if (level < minimum || level == LogLevel.kNone)
            return false;

        for (var i = 0; i < _backends.Length; i++)
        {
            if (_backendEnabled[i] && level >= _backends[i].MinimumLevel)
                return true;
        }

        return false;
    }

    public LogLevel MinimumLevel => (LogLevel)Volatile.Read(ref _minimumLevel);

    public void SetMinimumLevel(LogLevel level)
    {
        Volatile.Write(ref _minimumLevel, (int)level);
        _options.MinimumLevel = level;
    }

    public void SetCategoryMinimumLevel(string category, LogLevel level)
        => _categoryLevels[category] = level;

    public bool RemoveCategoryMinimumLevel(string category)
        => _categoryLevels.TryRemove(category, out _);

    public void UpdatePattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("Log pattern cannot be empty.", nameof(pattern));

        _options.Pattern = pattern;
        Volatile.Write(ref _formatter, new LogFormatter(pattern, _options.TimestampFormat));
    }

    internal void Write(
        LogLevel level,
        string name,
        string messageTemplate,
        in LogArguments arguments,
        Exception? exception)
    {
        if (!IsEnabled(level, name))
            return;

        var record = new LogRecord
        {
            Sequence = Interlocked.Increment(ref _sequence),
            Timestamp = DateTimeOffset.UtcNow,
            Level = level,
            Name = name,
            MessageTemplate = messageTemplate,
            Arguments = arguments,
            Exception = exception,
            ThreadId = Environment.CurrentManagedThreadId,
            ThreadName = _options.CaptureThreadName ? Thread.CurrentThread.Name : null
        };

        if (_queue.Writer.TryWrite(record))
        {
            Interlocked.Increment(ref _enqueued);
            return;
        }

        Interlocked.Increment(ref _dropped);
        Interlocked.Increment(ref _droppedTotal);
        if (level >= LogLevel.kError)
            EmergencyWrite($"[{level}] {name}: {messageTemplate}\n{exception}");
    }


    public LogStatistics GetStatistics()
    {
        var enqueued = Interlocked.Read(ref _enqueued);
        var processed = Interlocked.Read(ref _processed);
        return new LogStatistics(
            enqueued,
            processed,
            Interlocked.Read(ref _droppedTotal),
            Math.Max(0, enqueued - processed),
            _options.QueueCapacity);
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
            return;

        _queue.Writer.TryComplete();
        if (!_worker.Join(_options.ShutdownTimeout))
            EmergencyWrite("Kaon logger shutdown timed out before the queue drained.");

        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
    }

    private void Run()
    {
        var reader = _queue.Reader;
        var nextFlushAt = Environment.TickCount64 + (long)_options.FlushInterval.TotalMilliseconds;
        var pendingDropped = 0L;

        try
        {
            while (reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
            {
                var count = 0;
                var forceFlush = false;

                while (count < _options.BatchSize && reader.TryRead(out var record))
                {
                    WriteToOutputs(in record);
                    Interlocked.Increment(ref _processed);
                    forceFlush |= record.Level >= LogLevel.kError;
                    count++;
                }

                pendingDropped += Interlocked.Exchange(ref _dropped, 0);
                var now = Environment.TickCount64;
                if (pendingDropped > 0 && now >= nextFlushAt)
                {
                    var droppedRecord = new LogRecord
                    {
                        Sequence = Interlocked.Increment(ref _sequence),
                        Timestamp = DateTimeOffset.UtcNow,
                        Level = LogLevel.kWarn,
                        Name = "Kaon.Logger",
                        MessageTemplate = "Dropped {0} log records because the bounded queue was full.",
                        Arguments = LogArguments.Create(pendingDropped),
                        ThreadId = Environment.CurrentManagedThreadId,
                        ThreadName = Thread.CurrentThread.Name
                    };
                    WriteToOutputs(in droppedRecord);
                    pendingDropped = 0;
                    forceFlush = true;
                }

                if (!forceFlush && count >= _options.BatchSize && reader.TryPeek(out _) && now < nextFlushAt) 
                    continue;
                
                FlushOutputs();
                nextFlushAt = now + (long)_options.FlushInterval.TotalMilliseconds;
            }
        }
        catch (Exception e)
        {
            EmergencyWrite($"Kaon logger worker failed: {e}");
        }
        finally
        {
            pendingDropped += Interlocked.Exchange(ref _dropped, 0);
            if (pendingDropped > 0)
            {
                var droppedRecord = new LogRecord
                {
                    Sequence = Interlocked.Increment(ref _sequence),
                    Timestamp = DateTimeOffset.UtcNow,
                    Level = LogLevel.kWarn,
                    Name = "Kaon.Logger",
                    MessageTemplate = "Dropped {0} log records because the bounded queue was full.",
                    Arguments = LogArguments.Create(pendingDropped),
                    ThreadId = Environment.CurrentManagedThreadId,
                    ThreadName = Thread.CurrentThread.Name
                };
                WriteToOutputs(in droppedRecord);
            }

            FlushOutputs();
            StopOutputs();
        }
    }

    private void WriteToOutputs(in LogRecord record)
    {
        var formatted = Volatile.Read(ref _formatter).Format(in record);
        for (var i = 0; i < _backends.Length; i++)
        {
            if (!_backendEnabled[i] || record.Level < _backends[i].MinimumLevel)
                continue;

            try
            {
                _backends[i].Output(in record, formatted);
            }
            catch (Exception e)
            {
                _backendEnabled[i] = false;
                EmergencyWrite($"Kaon logger disabled output {_backends[i].GetType().Name}: {e}");
            }
        }
    }

    private void FlushOutputs()
    {
        for (var i = 0; i < _backends.Length; i++)
        {
            if (!_backendEnabled[i])
                continue;
            try
            {
                _backends[i].Flush();
            }
            catch (Exception e)
            {
                _backendEnabled[i] = false;
                EmergencyWrite($"Kaon logger disabled output {_backends[i].GetType().Name} during flush: {e}");
            }
        }
    }

    private void StopOutputs()
    {
        foreach (var t in _backends)
        {
            try
            {
                t.Stop();
            }
            catch (Exception e)
            {
                EmergencyWrite($"Kaon logger output stop failed: {t.GetType().Name}: {e}");
            }
        }
    }

    private static IOutput[] BuildDefaultOutputs(LogOptions options)
    {
        var outputs = new List<IOutput>(2);
        if (options.EnableConsole)
            outputs.Add(new LogPrinter());
        if (options.EnableFile)
            outputs.Add(new LogFilePrinter(minimumLevel: LogLevel.kTrace));
        return outputs.ToArray();
    }

    private static void EmergencyWrite(string message)
    {
        lock (sEmergencyLock)
        {
            try
            {
                Console.Error.WriteLine(message);
            }
            catch
            {
                // ignored
            }
        }
    }

    private void OnProcessExit(object? sender, EventArgs e) => Stop();
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e) => Stop();
}
