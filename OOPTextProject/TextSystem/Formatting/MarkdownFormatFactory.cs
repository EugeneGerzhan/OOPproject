using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OOPTextProject.TextSystem.Formatting;

/// <summary>
/// Family of formatters for Markdown: the root is <c>#</c>, headings are <c>##</c>..<c>######</c>
/// by nesting level, blocks are separated by a blank line. Element ids (if requested) are written
/// as HTML comments.
/// </summary>
public class MarkdownFormatFactory : ITextFormatFactory
{
    /// <inheritdoc />
    public string Name => "markdown";

    /// <inheritdoc />
    public string ChildSeparator => Environment.NewLine + Environment.NewLine;

    /// <inheritdoc />
    public IRootFormatter CreateRootFormatter() => new MarkdownRootFormatter();

    /// <inheritdoc />
    public IHeadingFormatter CreateHeadingFormatter() => new MarkdownHeadingFormatter();

    /// <inheritdoc />
    public IParagraphFormatter CreateParagraphFormatter() => new MarkdownParagraphFormatter();

    private static string IdComment(int id, bool showIds) => showIds ? $" <!-- id: {id} -->" : string.Empty;

    private static string WithChildren(string line, string children) =>
        children.Length == 0 ? line : line + ChildBreak + children;

    private static string ChildBreak => Environment.NewLine + Environment.NewLine;

    private sealed class MarkdownRootFormatter : IRootFormatter
    {
        public string Format(Root root, string renderedChildren, bool showIds) =>
            WithChildren($"# {root.Name}{IdComment(root.Id, showIds)}", renderedChildren);
    }

    private sealed class MarkdownHeadingFormatter : IHeadingFormatter
    {
        public string Format(Heading heading, int level, string renderedChildren, bool showIds)
        {
            string marks = new string('#', Math.Clamp(level + 1, 1, 6));
            return WithChildren($"{marks} {heading.Name}{IdComment(heading.Id, showIds)}", renderedChildren);
        }
    }

    private sealed class MarkdownParagraphFormatter : IParagraphFormatter
    {
        public string Format(Paragraph paragraph, int level, bool showIds) =>
            $"{paragraph.Content}{IdComment(paragraph.Id, showIds)}";
    }
}