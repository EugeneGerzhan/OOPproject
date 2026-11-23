using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OOPTextProject.TextSystem;

/// <summary>
/// Base interface for all text elements (pure model, no presentation logic).
/// It is the "Element" of the Visitor pattern.
/// </summary>
public interface IText
{
    int Id { get; }

    string Name { get; }

    /// <summary>Lets a visitor perform its operation on this element (see <see cref="ITextVisitor{T}"/>).</summary>
    T Accept<T>(ITextVisitor<T> visitor);
}