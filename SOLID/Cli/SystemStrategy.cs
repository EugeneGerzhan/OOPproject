using System;
using System.Collections.Generic;
using System.Linq;

namespace OOPproject.SOLID;

/// <summary>The commands that describe the command line itself: <c>commands</c> lists everything that can be typed.</summary>
public sealed class SystemStrategy : CommandStrategyBase
{
    private readonly Cli _cli;
    private readonly IPresenter _ui;

    public SystemStrategy(Cli cli, IPresenter ui)
    {
        _cli = cli;
        _ui = ui;

        Register("commands", HandleCommands);
    }

    // commands
    private void HandleCommands(string[] args, Dictionary<string, string> opts)
    {
        foreach (ICommandStrategy strategy in _cli.Strategies)
        {
            _ui.Show($"=== {strategy.GetType().Name} ===");
            _ui.Show("  " + string.Join(", ", strategy.GetCommandSelectors().OrderBy(s => s, StringComparer.OrdinalIgnoreCase)));
        }
    }
}