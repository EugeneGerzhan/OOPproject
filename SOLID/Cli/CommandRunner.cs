using System;
using System.Threading;
using System.Threading.Tasks;

namespace OOPproject.SOLID;

/// <summary>
/// Runs commands one after another on a worker thread. A graphical interface must not freeze while a command waits for
/// the network, and two commands must never work on the game at the same time: the runner does both.
/// </summary>
public sealed class CommandRunner
{
    private readonly Cli _cli;
    private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

    public CommandRunner(Cli cli)
    {
        _cli = cli ?? throw new ArgumentNullException(nameof(cli));
    }

    /// <summary>Runs a command line through the command line facade.</summary>
    public Task RunAsync(string commandLine) => RunAsync(() => _cli.ExecCommand(commandLine));

    /// <summary>Runs any piece of work in the same queue (for example a continue from the last save).</summary>
    public async Task RunAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(action).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}