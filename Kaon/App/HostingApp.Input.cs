using Kaon.Logger;

namespace Kaon.App;

public partial class HostingApp
{
    private Task? _consoleInputTask;
    private int _consoleInputStarted;

    private async Task ConsoleInputListener()
    {
        while (!_stopping.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = _commandDispatcher == null
                    ? await Console.In.ReadLineAsync(_stopping.Token).ConfigureAwait(false)
                    : await ReadConsoleLineWithCompletionAsync(_stopping.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (IOException)
            {
                return;
            }

            if (line == null)
            {
                StopApp();
                return;
            }

            try
            {
                if (_commandDispatcher != null)
                    await _commandDispatcher.DispatchAsync(line, _stopping.Token).ConfigureAwait(false);
                else
                    await OnConsoleInput(line).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to process console input.");
            }
        }
    }

    protected virtual Task OnConsoleInput(string line)
        => Task.CompletedTask;
    
    public void InitConsoleInputListener()
    {
        StartConsoleInputListener();
    }

    public void StopConsoleInputListener()
    {
        StopApp();
    }

    private void StartConsoleInputListener()
    {
        if (Interlocked.Exchange(ref _consoleInputStarted, 1) != 0)
            return;

        _consoleInputTask = Task.Run(ConsoleInputListener);
    }

    private async Task<string?> ReadConsoleLineWithCompletionAsync(CancellationToken cancellationToken)
    {
        if (Console.IsInputRedirected)
            return await Console.In.ReadLineAsync(cancellationToken).ConfigureAwait(false);

        var buffer = new List<char>();
        var completionIndex = -1;
        string[] completions = [];

        while (!cancellationToken.IsCancellationRequested)
        {
            var key = Console.ReadKey(intercept: true);
            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    return new string(buffer.ToArray());
                case ConsoleKey.Backspace:
                    if (buffer.Count > 0)
                    {
                        buffer.RemoveAt(buffer.Count - 1);
                        Console.Write("\b \b");
                    }
                    completionIndex = -1;
                    completions = [];
                    break;
                case ConsoleKey.Tab:
                {
                    var line = new string(buffer.ToArray());
                    if (completionIndex < 0)
                    {
                        completions = _commandDispatcher?.Complete(line).ToArray() ?? [];
                        completionIndex = 0;
                    }
                    else if (completions.Length > 0)
                    {
                        completionIndex = (completionIndex + 1) % completions.Length;
                    }

                    if (completions.Length == 0)
                        break;

                    ReplaceCurrentConsoleLine(buffer, completions[completionIndex]);
                    break;
                }
                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        buffer.Add(key.KeyChar);
                        Console.Write(key.KeyChar);
                        completionIndex = -1;
                        completions = [];
                    }
                    break;
            }
        }

        return null;
    }

    private static void ReplaceCurrentConsoleLine(List<char> buffer, string text)
    {
        while (buffer.Count > 0)
        {
            buffer.RemoveAt(buffer.Count - 1);
            Console.Write("\b \b");
        }

        buffer.AddRange(text);
        Console.Write(text);
    }
}
