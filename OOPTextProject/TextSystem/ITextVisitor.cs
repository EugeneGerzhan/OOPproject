using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OOPTextProject.TextSystem;

/// <summary>
/// VISITOR pattern: an operation over the document that is written OUTSIDE of the element classes.
/// </summary>
/// <remarks>
/// Before, every operation (formatting in <see cref="TextDto"/>, conversion in <see cref="TextMapper"/>)
/// had its own <c>switch</c> over the types of elements. Now every element accepts a visitor and calls the
/// method that belongs to its own type (double dispatch). A new operation is a new visitor and touches no
/// element class; a new kind of element adds a method here, so the compiler shows every visitor that must
/// be updated.
/// </remarks>
/// <typeparam name="T">The result of the operation for one element.</typeparam>
public interface ITextVisitor<T>
{
    T VisitRoot(Root root);

    T VisitHeading(Heading heading);

    T VisitParagraph(Paragraph paragraph);
}
