using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Linq;
using System.Net;

namespace OOPTextProject.TextSystem.Formatting;

/// <summary>
/// Family of formatters for HTML: the document is an <c>article</c>, headings are <c>section</c>
/// elements with <c>h1</c>..<c>h6</c> titles, paragraphs are <c>p</c>. Text is HTML-encoded and
/// element ids (if requested) are written to <c>data-id</c> attributes.
/// </summary>
public class HtmlFormatFactory : ITextFormatFactory
{
    private const int IndentSize = 2;

    /// <inheritdoc />
    public string Name => "html";

    /// <inheritdoc />
    public string ChildSeparator => Environment.NewLine;

    /// <inheritdoc />
    public IRootFormatter CreateRootFormatter() => new HtmlRootFormatter();

    /// <inheritdoc />
    public IHeadingFormatter CreateHeadingFormatter() => new HtmlHeadingFormatter();

    /// <inheritdoc />
    public IParagraphFormatter CreateParagraphFormatter() => new HtmlParagraphFormatter();

    private static string Indent(int level) => new string(' ', level * IndentSize);

    private static string IdAttribute(int id, bool showIds) => showIds ? $" data-id=\"{id}\"" : string.Empty;

    private static string Encode(string text) => WebUtility.HtmlEncode(text);

    private static int TitleTag(int level) => Math.Clamp(level + 1, 1, 6);

    // Joins non-empty lines (the children block is empty for an element without content).
    private static string Lines(params string[] lines) =>
        string.Join(Environment.NewLine, lines.Where(line => line.Length > 0));

    private sealed class HtmlRootFormatter : IRootFormatter
    {
        public string Format(Root root, string renderedChildren, bool showIds) => Lines(
            $"<article{IdAttribute(root.Id, showIds)}>",
            $"{Indent(1)}<h1>{Encode(root.Name)}</h1>",
            renderedChildren,
            "</article>");
    }

    private sealed class HtmlHeadingFormatter : IHeadingFormatter
    {
        public string Format(Heading heading, int level, string renderedChildren, bool showIds)
        {
            int tag = TitleTag(level);
            return Lines(
                $"{Indent(level)}<section{IdAttribute(heading.Id, showIds)}>",
                $"{Indent(level + 1)}<h{tag}>{Encode(heading.Name)}</h{tag}>",
                renderedChildren,
                $"{Indent(level)}</section>");
        }
    }

    private sealed class HtmlParagraphFormatter : IParagraphFormatter
    {
        public string Format(Paragraph paragraph, int level, bool showIds) =>
            $"{Indent(level)}<p{IdAttribute(paragraph.Id, showIds)}>{Encode(paragraph.Content)}</p>";
    }
}