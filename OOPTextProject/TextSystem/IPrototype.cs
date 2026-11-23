using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OOPTextProject.TextSystem;

/// <summary>
/// Prototype pattern: an element that can create a deep copy of itself.
/// </summary>
/// <remarks>
/// Every element of a document has a unique id, so a copy cannot simply reuse the ids of the original.
/// The classic <c>Clone()</c> is therefore extended with an id source: each copied element asks it for a fresh id.
/// </remarks>
/// <typeparam name="T">The type of the copy.</typeparam>
public interface IPrototype<out T>
{
    /// <summary>Creates a deep copy of the element.</summary>
    /// <param name="nextId">Supplies a new unique id for every element of the copy.</param>
    /// <returns>The copy (without a parent).</returns>
    T Clone(Func<int> nextId);
}
