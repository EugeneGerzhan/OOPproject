using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace OOPTextProject.TextSystem;

/// <summary>
/// The caretaker of the Memento pattern: keeps the snapshots and gives them back, the last one first.
/// It never looks inside a <see cref="TextMemento"/>. When the history is full, the oldest snapshot is forgotten.
/// </summary>
public sealed class UndoHistory
{
    private readonly List<TextMemento> _snapshots = new List<TextMemento>();
    private readonly int _capacity;

    /// <param name="capacity">How many steps can be undone at most.</param>
    public UndoHistory(int capacity = 50)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "The capacity must be at least 1.");
        }
        _capacity = capacity;
    }

    /// <summary>How many steps can be undone now.</summary>
    public int Count => _snapshots.Count;

    public void Push(TextMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);

        _snapshots.Add(memento);
        if (_snapshots.Count > _capacity)
        {
            _snapshots.RemoveAt(0);
        }
    }

    /// <summary>Takes the last snapshot out of the history. Returns false when the history is empty.</summary>
    public bool TryPop([NotNullWhen(true)] out TextMemento? memento)
    {
        if (_snapshots.Count == 0)
        {
            memento = null;
            return false;
        }

        memento = _snapshots[^1];
        _snapshots.RemoveAt(_snapshots.Count - 1);
        return true;
    }

    public void Clear() => _snapshots.Clear();
}