using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;

namespace OOPTextProject.TextSystem;

/// <summary>
/// Heading element that acts as a container. Can be copied together with all its content (Prototype).
/// </summary>
public class Heading : Container, IPrototype<IText>
{
    public Heading(int id, string name) : base(id, name) { }

    /// <inheritdoc />
    public IText Clone(Func<int> nextId) => CloneAs(Name, nextId);

    /// <summary>
    /// Creates a deep copy of this heading under a new title. Only the title of the top heading changes,
    /// all nested elements keep their names.
    /// </summary>
    /// <param name="newName">The title of the copy (must already be valid).</param>
    /// <param name="nextId">Supplies a new unique id for every element of the copy.</param>
    public Heading CloneAs(string newName, Func<int> nextId)
    {
        ArgumentNullException.ThrowIfNull(nextId);

        var copy = new Heading(nextId(), newName);
        foreach (IText child in Children)
        {
            if (child is not IPrototype<IText> prototype)
            {
                throw new InvalidOperationException($"'{child.Name}' cannot be copied.");
            }
            copy.AddChild(prototype.Clone(nextId));
        }

        return copy;
    }

    /// <inheritdoc />
    public override T Accept<T>(ITextVisitor<T> visitor) => visitor.VisitHeading(this);
}