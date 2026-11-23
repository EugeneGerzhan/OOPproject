using System;
using System.Collections.Generic;
using Infra.Display;

namespace OOPproject.SOLID;

/// <summary>What the strategies need from the command line to show a renderable.</summary>
public interface IRenderableDisplay
{
    void Display<T>(IRenderable<T> renderable);
}

/// <summary>
/// FACADE of the command line (the "CLI" of the design). Outside code uses only three operations:
/// <see cref="UseStrategy"/> to plug a set of commands in, <see cref="ExecCommand"/> to run a line typed
/// by the user and <see cref="Display{T}"/> to show something. Parsing, choosing the strategy, error reporting
/// and output are hidden inside.
/// </summary>
public class Cli : IRenderableDisplay
{
    private readonly IPresenter _presenter;
    private readonly IArgsParser _parser;
    private readonly List<ICommandStrategy> _strategies = new List<ICommandStrategy>();
    private readonly Dictionary<string, ICommandStrategy> _owners = new Dictionary<string, ICommandStrategy>(StringComparer.OrdinalIgnoreCase);

    public Cli(IPresenter presenter, IArgsParser? parser = null)
    {
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _parser = parser ?? new ArgsParser();
    }

    /// <summary>The strategies in the order they were plugged in.</summary>
    public IReadOnlyList<ICommandStrategy> Strategies => _strategies;

    /// <summary>
    /// Plugs a strategy in: its commands become available. If another strategy already has a command with the
    /// same selector, the one plugged in later wins.
    /// </summary>
    public void UseStrategy(ICommandStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        if (!_strategies.Contains(strategy))
        {
            _strategies.Add(strategy);
        }
        foreach (string selector in strategy.GetCommandSelectors())
        {
            _owners[selector] = strategy;
        }
    }

    /// <summary>
    /// Runs a line typed by the user: parses it, finds the strategy that owns the command and executes it.
    /// Errors of the command are shown to the user instead of being thrown.
    /// </summary>
    public void ExecCommand(string rawInput)
    {
        ParsedCommand? parsed = _parser.ParseArgs(rawInput);
        if (parsed == null)
        {
            return;
        }

        if (!_owners.TryGetValue(parsed.Selector, out var strategy))
        {
            _presenter.Show($"Unknown command: '{parsed.Selector}'. Type 'help' for available commands.");
            return;
        }

        try
        {
            strategy.ExecCommand(parsed.Selector, parsed.Args);
        }
        catch (Exception ex)
        {
            _presenter.Show($"[Error executing command]: {ex.Message}");
        }
    }

    /// <summary>Shows a renderable with the renderer it carries.</summary>
    /// <exception cref="InvalidOperationException">The renderable has no renderer.</exception>
    public void Display<T>(IRenderable<T> renderable)
    {
        ArgumentNullException.ThrowIfNull(renderable);
        if (!renderable.HasRenderer)
        {
            throw new InvalidOperationException($"No renderer is set for {typeof(T).Name}.");
        }

        _presenter.Show(renderable);
    }

    /// <summary>Shows an object that renders itself (the DTOs of the core).</summary>
    public void Display(IDisplayable displayable) => _presenter.Show(displayable);
}