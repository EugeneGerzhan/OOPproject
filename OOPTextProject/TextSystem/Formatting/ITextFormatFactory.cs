using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OOPTextProject.TextSystem.Formatting;

/// <summary>Abstract product: presents the root of a document in a concrete format.</summary>
public interface IRootFormatter
{
    /// <param name="root">The root element.</param>
    /// <param name="renderedChildren">Already formatted content of the document (may be empty).</param>
    /// <param name="showIds">Whether element ids must be shown.</param>
    string Format(Root root, string renderedChildren, bool showIds);
}

/// <summary>Abstract product: presents a heading in a concrete format.</summary>
public interface IHeadingFormatter
{
    /// <param name="heading">The heading element.</param>
    /// <param name="level">Nesting level relative to the printed element (0 for the printed element itself).</param>
    /// <param name="renderedChildren">Already formatted content of the heading (may be empty).</param>
    /// <param name="showIds">Whether element ids must be shown.</param>
    string Format(Heading heading, int level, string renderedChildren, bool showIds);
}

/// <summary>Abstract product: presents a paragraph in a concrete format.</summary>
public interface IParagraphFormatter
{
    /// <param name="paragraph">The paragraph element.</param>
    /// <param name="level">Nesting level relative to the printed element.</param>
    /// <param name="showIds">Whether element ids must be shown.</param>
    string Format(Paragraph paragraph, int level, bool showIds);
}

/// <summary>
/// Abstract Factory pattern: creates a family of formatters that present all kinds of document
/// elements in one consistent format (plain text, HTML, Markdown...).
/// </summary>
/// <remarks>
/// <see cref="TextDto"/> works only with this interface, so a new format is added by writing one new
/// factory and registering it in <see cref="TextFormats"/>, without touching the existing code.
/// </remarks>
public interface ITextFormatFactory
{
    /// <summary>Short name of the format, used by the command line ("plain", "html", ...).</summary>
    string Name { get; }

    /// <summary>Text that separates formatted sibling elements (a blank line is needed in Markdown).</summary>
    string ChildSeparator { get; }

    /// <summary>Creates the formatter of the document root.</summary>
    IRootFormatter CreateRootFormatter();

    /// <summary>Creates the formatter of headings.</summary>
    IHeadingFormatter CreateHeadingFormatter();

    /// <summary>Creates the formatter of paragraphs.</summary>
    IParagraphFormatter CreateParagraphFormatter();
}