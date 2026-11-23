using System;
using System.Collections.Generic;
using OOPTextProject.TextSystem;

namespace OOPproject.SOLID;

/// <summary>
/// COMMAND + MEMENTO: wraps another command and makes it undoable.
/// </summary>
/// <remarks>
/// Before the command runs, a snapshot of the document is taken. If the command really changed the document
/// (see <see cref="TextFactory.Revision"/>), the snapshot goes to the <see cref="UndoHistory"/>. A command that
/// only prints a message, is cancelled or fails leaves no trace in the history. The wrapped command knows
/// nothing about undo, so the command classes stay simple and undo is added in one place.
/// </remarks>
public sealed class UndoableCommand : ICommand
{
    private readonly ICommand _inner;
    private readonly TextFactory _document;
    private readonly UndoHistory _history;

    public UndoableCommand(ICommand inner, TextFactory document, UndoHistory history)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _history = history ?? throw new ArgumentNullException(nameof(history));
    }

    public void Execute(string[] args, Dictionary<string, string> options)
    {
        TextMemento before = _document.CreateMemento();
        int revision = _document.Revision;

        _inner.Execute(args, options);

        if (_document.Revision != revision)
        {
            _history.Push(before);
        }
    }
}
