using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OOPTextProject.TextSystem.Formatting;

/// <summary>
/// Family of formatters for plain text: one line per element, indented by nesting level.
/// This is the original format of the system.
/// </summary>
public class PlainTextFormatFactory : ITextFormatFactory
{
    private const int IndentSize = 4;

    /// <inheritdoc />
    public string Name => "plain";

    /// <inheritdoc />
    public string ChildSeparator => Environment.NewLine;

    /// <inheritdoc />
    public IRootFormatter CreateRootFormatter() => new PlainRootFormatter();

    /// <inheritdoc />
    public IHeadingFormatter CreateHeadingFormatter() => new PlainHeadingFormatter();

    /// <inheritdoc />
    public IParagraphFormatter CreateParagraphFormatter() => new PlainParagraphFormatter();

    private static string Line(int level, bool showIds, int id, string text) =>
        new string(' ', level * IndentSize) + (showIds ? $"[{id}] " : string.Empty) + text;

    private static string WithChildren(string line, string children) =>
        children.Length == 0 ? line : line + Environment.NewLine + children;

    private sealed class PlainRootFormatter : IRootFormatter
    {
        public string Format(Root root, string renderedChildren, bool showIds) =>
            WithChildren(Line(0, showIds, root.Id, $"Title: {root.Name}"), renderedChildren);
    }

    private sealed class PlainHeadingFormatter : IHeadingFormatter
    {
        public string Format(Heading heading, int level, string renderedChildren, bool showIds) =>
            WithChildren(Line(level, showIds, heading.Id, heading.Name), renderedChildren);
    }

    private sealed class PlainParagraphFormatter : IParagraphFormatter
    {
        public string Format(Paragraph paragraph, int level, bool showIds) =>
            Line(level, showIds, paragraph.Id, paragraph.Content);
    }
}
