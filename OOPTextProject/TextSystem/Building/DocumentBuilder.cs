using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OOPTextProject.TextSystem.Building;

/// <summary>
/// Builder pattern: assembles a whole document (a tree of headings and paragraphs) step by step
/// and hands out the finished <see cref="Root"/> with <see cref="Build"/>.
/// </summary>
/// <remarks>
/// The builder hides the bookkeeping that building a tree by hand requires: which container is open,
/// the next free id and the validation of titles. A document that is not finished correctly
/// (an unclosed heading) can never be obtained. A builder is single-use: after
/// <see cref="Build"/> it refuses further calls.
/// </remarks>
public class DocumentBuilder
{
    private readonly Root _root;
    private readonly Stack<Container> _open = new();
    private int _nextId = 1; // the root has id 0
    private bool _built;

    /// <summary>Starts a document.</summary>
    /// <param name="title">The title of the document (the root).</param>
    /// <exception cref="ArgumentException">The title is empty.</exception>
    public DocumentBuilder(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Document title cannot be empty.", nameof(title));
        }

        _root = new Root(title.Trim());
        _open.Push(_root);
    }

    /// <summary>Opens a new heading inside the current container; following calls add content to it.</summary>
    public DocumentBuilder BeginHeading(string name)
    {
        EnsureNotBuilt();

        string valid = TextRules.ValidateHeadingName(name);
        var heading = new Heading(_nextId++, valid);
        _open.Peek().AddChild(heading);
        _open.Push(heading);
        return this;
    }

    /// <summary>Closes the innermost open heading.</summary>
    /// <exception cref="InvalidOperationException">There is no open heading.</exception>
    public DocumentBuilder EndHeading()
    {
        EnsureNotBuilt();

        if (_open.Count == 1)
        {
            throw new InvalidOperationException("There is no open heading to end.");
        }

        _open.Pop();
        return this;
    }

    /// <summary>Adds a paragraph to the current container.</summary>
    public DocumentBuilder AddParagraph(string content)
    {
        EnsureNotBuilt();

        string valid = TextRules.ValidateParagraph(content);
        _open.Peek().AddChild(new Paragraph(_nextId++, valid));
        return this;
    }

    /// <summary>
    /// Adds a heading with its content in one call; the heading is closed automatically,
    /// so the structure cannot become unbalanced.
    /// </summary>
    public DocumentBuilder AddHeading(string name, Action<DocumentBuilder> content)
    {
        ArgumentNullException.ThrowIfNull(content);

        BeginHeading(name);
        content(this);
        return EndHeading();
    }

    /// <summary>Finishes the document.</summary>
    /// <returns>The root of the finished document.</returns>
    /// <exception cref="InvalidOperationException">A heading is still open or the document is already built.</exception>
    public Root Build()
    {
        EnsureNotBuilt();

        if (_open.Count > 1)
        {
            throw new InvalidOperationException(
                $"Heading '{_open.Peek().Name}' is not closed. Call EndHeading() before Build().");
        }

        _built = true;
        return _root;
    }

    private void EnsureNotBuilt()
    {
        if (_built)
        {
            throw new InvalidOperationException("The document is already built.");
        }
    }
}