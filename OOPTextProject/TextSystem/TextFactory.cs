using System.Linq;
using Infra.Data;
using Infra.Display;
using OOPTextProject.TextSystem.Formatting;

namespace OOPTextProject.TextSystem;

/// <summary>
/// Facade of the text system: creates elements and tracks the current position.
/// It is also the ORIGINATOR of the Memento pattern (<see cref="CreateMemento"/>, <see cref="Restore"/>).
/// Contains no console I/O: views are returned as IDisplayable.
/// </summary>
public class TextFactory
{
    private Root _root;
    private Container _current;
    private int _nextId = 1; // root has id 0

    public TextFactory(string title)
    {
        _root = new Root(title);
        _current = _root;
    }

    public Container Current => _current;

    /// <summary>The whole document (used for saving).</summary>
    public Root Document => _root;

    /// <summary>
    /// How many times the document has been changed (elements added, removed or copied, the document loaded).
    /// Moving around the document does not change it. Commands use it to find out whether a change happened.
    /// </summary>
    public int Revision { get; private set; }

    /// <summary>Replaces the whole document (used for loading). Current position becomes the root.</summary>
    public void Load(Root root)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _current = root;
        _nextId = MaxId(root) + 1;
        Revision++;
    }

    // ---------- memento ----------

    /// <summary>Takes a snapshot of the document and of the current position.</summary>
    public TextMemento CreateMemento()
    {
        TextNodeData snapshot = TextMapper.ToSave(string.Empty, _root).TextRoot!;
        return new TextMemento(snapshot, _current.Id);
    }

    /// <summary>Brings the document and the current position back to the state of a snapshot.</summary>
    public void Restore(TextMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);

        Load(TextMapper.ToRoot(new TextSave { TextRoot = memento.Document }));
        if (_root.FindById(memento.CurrentId) is Container current)
        {
            _current = current;
        }
    }

    // ---------- creation ----------

    /// <summary>Adds a heading to the current container and makes it the current one.</summary>
    public Heading AddHeading(string name)
    {
        string valid = TextRules.ValidateHeadingName(name);

        var heading = new Heading(_nextId++, valid);
        _current.AddChild(heading);
        _current = heading;
        Revision++;
        return heading;
    }

    /// <summary>Adds a paragraph to the current container.</summary>
    public Paragraph AddParagraph(string content)
    {
        string valid = TextRules.ValidateParagraph(content);

        var paragraph = new Paragraph(_nextId++, valid);
        _current.AddChild(paragraph);
        Revision++;
        return paragraph;
    }

    /// <summary>
    /// Copies an element (with its whole subtree) into the current container using the Prototype pattern.
    /// Every copied element gets a new unique id. The current position does not change.
    /// </summary>
    /// <param name="source">The element to copy; it may be located anywhere in the document.</param>
    /// <param name="newName">The title of the copy (headings only; "&lt;name&gt; (copy)" when omitted).</param>
    /// <returns>The top element of the copy.</returns>
    /// <exception cref="InvalidOperationException">The element (for example the root) cannot be copied.</exception>
    /// <exception cref="ArgumentException">The new title is not valid.</exception>
    public IText AddCopy(IText source, string? newName = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is not IPrototype<IText> prototype)
        {
            throw new InvalidOperationException($"{source.GetType().Name} '{source.Name}' cannot be copied.");
        }

        IText copy = source is Heading heading
            ? heading.CloneAs(TextRules.ValidateHeadingName(newName ?? $"{heading.Name} (copy)"), () => _nextId++)
            : prototype.Clone(() => _nextId++);

        _current.AddChild(copy);
        Revision++;
        return copy;
    }

    // ---------- navigation ----------

    /// <summary>Moves to the parent container. Returns false if already in root.</summary>
    public bool Up()
    {
        if (_current.Parent == null)
        {
            return false;
        }
        _current = _current.Parent;
        return true;
    }

    /// <summary>
    /// Moves to a container by path. Absolute paths start with '/', others are relative to the
    /// current container. Supports '.' and '..'. The position changes only if the whole path resolves.
    /// </summary>
    public bool TryCd(string path, out string error)
    {
        error = string.Empty;
        path = (path ?? string.Empty).Trim();
        if (path.Length == 0)
        {
            error = "Path cannot be empty.";
            return false;
        }

        Container node = path.StartsWith("/") ? _root : _current;

        foreach (var rawSegment in path.Split('/'))
        {
            string segment = rawSegment.Trim();
            if (segment.Length == 0 || segment == ".")
            {
                continue;
            }
            if (segment == "..")
            {
                node = node.Parent ?? node;
                continue;
            }

            var matches = node.Children
                .OfType<Container>()
                .Where(c => string.Equals(c.Name, segment, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
            {
                error = $"Container '{segment}' not found in '{BuildPath(node)}'.";
                return false;
            }
            if (matches.Count > 1)
            {
                error = $"Several containers are named '{segment}'. Use: cd --id <container_id> (see 'print --id').";
                return false;
            }
            node = matches[0];
        }

        _current = node;
        return true;
    }

    /// <summary>Moves to the container with the given id.</summary>
    public bool TryCdById(int id, out string error)
    {
        error = string.Empty;
        var found = _root.FindById(id);

        if (found == null)
        {
            error = $"Element with id {id} not found.";
            return false;
        }
        if (found is not Container container)
        {
            error = $"Element with id {id} is not a container.";
            return false;
        }

        _current = container;
        return true;
    }

    public string GetCurrentPath() => BuildPath(_current);

    // ---------- find / remove ----------

    /// <summary>Finds a direct child of the current container by name.</summary>
    public bool TryFindChild(string name, out IText? element, out string error)
    {
        element = null;
        error = string.Empty;

        var matches = _current.Children
            .Where(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            error = $"Element '{name}' not found in the current element.";
            return false;
        }
        if (matches.Count > 1)
        {
            error = $"Several elements are named '{name}'. Use: rm --id <element_id> (see 'print --id').";
            return false;
        }

        element = matches[0];
        return true;
    }

    /// <summary>Finds any element of the document by id.</summary>
    public bool TryFindById(int id, out IText? element, out string error)
    {
        error = string.Empty;
        element = _root.FindById(id);
        if (element == null)
        {
            error = $"Element with id {id} not found.";
            return false;
        }
        return true;
    }

    /// <summary>
    /// Removes the element from the document. If the current container is inside the
    /// removed element, the current position moves to the removed element's parent.
    /// </summary>
    public bool TryRemove(IText element, out string error)
    {
        error = string.Empty;

        if (ReferenceEquals(element, _root))
        {
            error = "Root cannot be removed.";
            return false;
        }

        var parent = _root.FindParentOf(element.Id);
        if (parent == null)
        {
            error = "Element is not part of the document.";
            return false;
        }

        bool currentAffected = element is Container removed && IsSameOrAncestor(removed, _current);

        parent.RemoveChild(element);
        Revision++;

        if (currentAffected)
        {
            _current = parent;
        }
        return true;
    }

    // ---------- views ----------

    /// <summary>The whole document in the given format (plain text by default).</summary>
    public IDisplayable GetWholeView(bool showIds = false, ITextFormatFactory? formats = null) =>
        new TextDto(_root, showIds, formats);

    /// <summary>The current element in the given format (plain text by default).</summary>
    public IDisplayable GetCurrentView(bool showIds = false, ITextFormatFactory? formats = null) =>
        new TextDto(_current, showIds, formats);

    public override string ToString() => new TextDto(_root, false).Render();

    // ---------- helpers ----------

    private static string BuildPath(Container node)
    {
        var names = new Stack<string>();
        for (Container? n = node; n != null && n.Parent != null; n = n.Parent)
        {
            names.Push(n.Name);
        }
        return "/" + string.Join("/", names);
    }

    private static bool IsSameOrAncestor(Container candidate, Container node)
    {
        for (Container? n = node; n != null; n = n.Parent)
        {
            if (ReferenceEquals(n, candidate))
            {
                return true;
            }
        }
        return false;
    }

    private static int MaxId(Container container) => container.SelfAndDescendants().Max(node => node.Id);
}