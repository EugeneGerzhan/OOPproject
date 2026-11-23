using System;
using System.Collections.Generic;

namespace OOPproject.SOLID;

/// <summary>
/// COMMAND pattern: a terminal command as an object.
/// </summary>
/// <remarks>
/// Roles: <see cref="ICommand"/> is the command, <see cref="DelegateCommand"/> and
/// <see cref="UndoableCommand"/> are concrete commands, <see cref="CommandInterpreter"/> is the invoker
/// (it finds the command by name and runs it), the handlers (<c>TextCliHandler</c> and others) are the clients
/// that create and register commands, and the objects the handlers work with (for example the text factory)
/// are the receivers. The command is combined with the delegates of C#: an ordinary method can still be
/// registered, it is wrapped in a <see cref="DelegateCommand"/>.
/// </remarks>
public interface ICommand
{
    void Execute(string[] args, Dictionary<string, string> options);
}

/// <summary>A command that runs a method (an existing handler or a lambda).</summary>
public sealed class DelegateCommand : ICommand
{
    private readonly Action<string[], Dictionary<string, string>> _action;

    public DelegateCommand(Action<string[], Dictionary<string, string>> action)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    public void Execute(string[] args, Dictionary<string, string> options) => _action(args, options);
}