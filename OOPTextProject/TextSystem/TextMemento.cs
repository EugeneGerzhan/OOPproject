using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Infra.Data;

namespace OOPTextProject.TextSystem;

/// <summary>
/// MEMENTO pattern: a snapshot of a <see cref="TextFactory"/> (the whole document and the current position).
/// </summary>
/// <remarks>
/// Only the originator (<see cref="TextFactory"/>) can read the snapshot: its members are
/// <c>internal</c>. The caretaker (<see cref="UndoHistory"/>) and the command line can keep it and give it
/// back, but they can neither look inside nor change it, so the encapsulation of the document is not broken.
/// The snapshot is made of immutable data, so later changes of the document never touch it.
/// </remarks>
public sealed class TextMemento
{
    internal TextMemento(TextNodeData document, int currentId)
    {
        Document = document;
        CurrentId = currentId;
    }

    internal TextNodeData Document { get; }

    internal int CurrentId { get; }
}
