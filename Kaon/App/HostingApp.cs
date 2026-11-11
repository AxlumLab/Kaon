using Kaon.Command;
using Kaon.Logger;

namespace Kaon.App;

public partial class HostingApp
{
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _stopping = new();
    private readonly TaskCompletionSource _shutdown = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _runLock = new();
    private HostState _state = HostState.None;
    private int _stopInvoked;
    private int _commandLineHandled;
    private CommandDispatcher? _commandDispatcher;
    private CommandRegistry? _commandRegistry;

    private static HostingApp? _appInstance;
    private static readonly Lock _appLock = new();

    private static bool SetInstance(HostingApp app)
    {
        if (_appInstance != null) return false;
        lock (_appLock)
        {
            if (_appInstance == null) _appInstance = app;
            else return false;
        }

        return true;
    }

    public static HostingApp GetInstance()
    {
        if (_appInstance == null) throw new InvalidDataException("App instance are not initilized.");
        return _appInstance;
    }

    public static T GetInstance<T>() where T : HostingApp
    {
        return (T)GetInstance();
    }

    public CmdLine CommandLine { get; }
    public IReadOnlyList<string> UnhandledArgs => CommandLine.GetUnhandledArgs();

    public void UseCommands(ICommandMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        _commandRegistry ??= new CommandRegistry();
        mapper.RegisterCommands(_commandRegistry);
        _commandDispatcher = new CommandDispatcher(_commandRegistry, this);
    }

    public virtual void OnStop() {}

    protected virtual void OnUnhandledCommandLineArgs(IReadOnlyList<string> args)
    {
        if (args.Count > 0)
            _logger.Warn("Unhandled command line arguments: {0}", string.Join(' ', args));
    }

    private enum HostState
    {
        None, Sync, Async, End
    }
    
    protected HostingApp(string[] args)
    {
        if (!SetInstance(this)) 
            throw new Exception("Already init hosting app.");
        
        CommandLine = CmdLine.Parse(args);
        _logger = LogManager.GetLogger();
    }
    
    public HostingApp()
    {
        if (!SetInstance(this)) 
            throw new Exception("Already init hosting app.");
        
        CommandLine = CmdLine.Parse([]);
        _logger = LogManager.GetLogger();
    }

    public void RunApp()
    {
        if (!TryStart(HostState.Sync))
            return;

        using var registration = RegisterConsoleCancel();
        HandleUnhandledCommandLineArgs();
        StartConsoleInputListener();

        try
        {
            _shutdown.Task.GetAwaiter().GetResult();
        }
        finally
        {
            StopCore();
        }
    }

    public async Task RunAppAsync()
    {
        if (!TryStart(HostState.Async))
            return;

        using var registration = RegisterConsoleCancel();
        HandleUnhandledCommandLineArgs();
        StartConsoleInputListener();
        
        try
        {
            await _shutdown.Task.ConfigureAwait(false);
        }
        catch(Exception e)
        {
            _logger.Error(e, "Failed to host helium app!");
        }
        finally
        {
            StopCore();
        }
    }

    public void StopApp()
    {
        if (_state is HostState.None or HostState.End)
            return;

        try
        {
            _stopping.Cancel();
            _shutdown.TrySetResult();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void EnsureStopped()
    {
        StopCore();
    }

    private bool TryStart(HostState state)
    {
        lock (_runLock)
        {
            if (_state == HostState.None)
            {
                _state = state;
                return true;
            }
        }

        _logger.Error("Hosting app failed! Current app is not in none host state.");
        return false;
    }

    private IDisposable RegisterConsoleCancel()
    {
        ConsoleCancelEventHandler handler = (_, e) =>
        {
            _logger.Debug("Console shutdown requested.");
            e.Cancel = true;
            StopApp();
        };

        Console.CancelKeyPress += handler;
        return new ConsoleCancelRegistration(handler);
    }

    private void HandleUnhandledCommandLineArgs()
    {
        if (Interlocked.Exchange(ref _commandLineHandled, 1) != 0)
            return;

        OnUnhandledCommandLineArgs(CommandLine.GetUnhandledArgs());
    }

    private void StopCore()
    {
        if (Interlocked.Exchange(ref _stopInvoked, 1) != 0)
            return;

        _state = HostState.End;
        StopConsoleInputListener();
        try
        {
            OnStop();
        }
        finally
        {
            try
            {
                StopServicesAsync().AsTask().GetAwaiter().GetResult();
            }
            finally
            {
                try
                {
                    SaveConfigs();
                }
                finally
                {
                    LogManager.Shutdown();
                }
            }
        }
    }

    private sealed class ConsoleCancelRegistration(ConsoleCancelEventHandler handler) : IDisposable
    {
        public void Dispose()
        {
            Console.CancelKeyPress -= handler;
        }
    }
}
