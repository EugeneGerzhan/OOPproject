using System.Linq;
using Infra.Display;
using OOPTextProject.TextSystem.Formatting;

namespace OOPTextProject.TextSystem;

/// <summary>
/// DTO that turns a text element (with all its descendants) into a displayable string.
/// </summary>
/// <remarks>
/// It is a VISITOR (it walks the tree with <see cref="IText.Accept{T}"/> instead of checking types)
/// and the client of the Abstract Factory: the presentation of every element is delegated to the formatters
/// created by an <see cref="ITextFormatFactory"/>. A visitor keeps the nesting level while it walks,
/// so one instance must not render from several threads at once.
/// </remarks>
public class TextDto : IDisplayable, ITextVisitor<string>
{
    private readonly IText _node;
    private readonly bool _showIds;
    private readonly string _separator;
    private readonly IRootFormatter _rootFormatter;
    private readonly IHeadingFormatter _headingFormatter;
    private readonly IParagraphFormatter _paragraphFormatter;

    // Nesting level of the element that is being visited (0 for the printed element itself).
    private int _level;

    /// <param name="node">The element to present (with its subtree).</param>
    /// <param name="showIds">Whether element ids must be shown.</param>
    /// <param name="formats">The format family; plain text when omitted.</param>
    public TextDto(IText node, bool showIds, ITextFormatFactory? formats = null)
    {
        ITextFormatFactory factory = formats ?? TextFormats.Plain;

        _node = node;
        _showIds = showIds;
        _separator = factory.ChildSeparator;
        _rootFormatter = factory.CreateRootFormatter();
        _headingFormatter = factory.CreateHeadingFormatter();
        _paragraphFormatter = factory.CreateParagraphFormatter();
    }

    public string Render()
    {
        _level = 0;
        return _node.Accept(this);
    }

    public string VisitRoot(Root root) =>
        _rootFormatter.Format(root, VisitChildren(root), _showIds);

    public string VisitHeading(Heading heading)
    {
        int level = _level;
        return _headingFormatter.Format(heading, level, VisitChildren(heading), _showIds);
    }

    public string VisitParagraph(Paragraph paragraph) =>
        _paragraphFormatter.Format(paragraph, _level, _showIds);

    private string VisitChildren(Container container)
    {
        _level++;
        try
        {
            return string.Join(_separator, container.Children.Select(child => child.Accept(this)));
        }
        finally
        {
            _level--;
        }
    }
}