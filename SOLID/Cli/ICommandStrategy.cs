using System;
using System.Collections.Generic;
using System.Linq;

namespace OOPproject.SOLID;

/// <summary>
/// STRATEGY of the command line: a set of commands that belong together (the commands of the text editor,
/// of the characters, of the game...). The <see cref="Cli"/> is parametrized with strategies and sends every
/// command to the strategy that owns its selector.
/// </summary>
public interface ICommandStrategy
{
    /// <summary>The names of the commands this strategy can execute.</summary>
    IReadOnlyCollection<string> GetCommandSelectors();

    /// <summary>Adds a command under a selector (replacing the command registered earlier under it).</summary>
    void RegisterCommand(string selector, ICommand command);

    /// <summary>Executes the command with the given selector.</summary>
    /// <exception cref="InvalidOperationException">The strategy has no such command.</exception>
    void ExecCommand(string selector, Args args);
}

/// <summary>
/// Base class of the strategies. It keeps the table "selector -> command"; a concrete strategy only registers
/// its commands (methods, or <see cref="ICommand"/> objects) in its constructor.
/// </summary>
public abstract class CommandStrategyBase : ICommandStrategy
{
    private readonly Dictionary<string, ICommand> _commands = new Dictionary<string, ICommand>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> GetCommandSelectors() => _commands.Keys.ToList();

    public void RegisterCommand(string selector, ICommand command)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            throw new ArgumentException("The selector of a command cannot be empty.", nameof(selector));
        }
        ArgumentNullException.ThrowIfNull(command);

        _commands[selector] = command;
    }

    public void ExecCommand(string selector, Args args)
    {
        if (!_commands.TryGetValue(selector, out var command))
        {
            throw new InvalidOperationException($"{GetType().Name} has no command '{selector}'.");
        }

        command.Execute(args.Positional, args.Options);
    }

    /// <summary>Compatibility with older code: <c>handler.SetupCommands(cli)</c> is the same as <c>cli.UseStrategy(handler)</c>.</summary>
    public void SetupCommands(Cli cli) => cli.UseStrategy(this);

    /// <summary>Registers a method as a command.</summary>
    protected void Register(string selector, Action<string[], Dictionary<string, string>> action) =>
        RegisterCommand(selector, new DelegateCommand(action));

    /// <summary>Registers a command object.</summary>
    protected void Register(string selector, ICommand command) => RegisterCommand(selector, command);
}