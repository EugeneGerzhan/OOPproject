using System;
using System.Collections.Generic;
using System.Text;

using System.Collections.Generic;
using System.Linq;

namespace OOPTextProject.TextSystem;

/// <summary>Abstract container class that can hold child elements (Composite pattern).</summary>
public abstract class Container : IText
{
    private readonly List<IText> _children = new List<IText>();

    public int Id { get; }

    public string Name { get; }

    public Container? Parent { get; private set; }

    public IReadOnlyList<IText> Children => _children;

    protected Container(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public bool IsRoot() => Parent == null;

    public void AddChild(IText child)
    {
        if (child is Container container)
        {
            container.Parent = this;
        }
        _children.Add(child);
    }

    public bool RemoveChild(IText child)
    {
        if (!_children.Remove(child))
        {
            return false;
        }
        if (child is Container container)
        {
            container.Parent = null;
        }
        return true;
    }

    /// <summary>
    /// ITERATOR pattern (the C# way: an iterator method with <c>yield</c>).
    /// Lazily walks this container and everything below it, depth first, a container before its children.
    /// </summary>
    /// <remarks>
    /// One walk instead of a hand-written recursion in every place that needs to search the tree:
    /// finding an element, finding a parent, the largest id and the number of elements are all
    /// simple LINQ queries over this sequence, and the caller never sees how the tree is stored.
    /// </remarks>
    public IEnumerable<IText> SelfAndDescendants()
    {
        yield return this;

        foreach (IText child in _children)
        {
            if (child is Container container)
            {
                foreach (IText inner in container.SelfAndDescendants())
                {
                    yield return inner;
                }
            }
            else
            {
                yield return child;
            }
        }
    }

    /// <summary>Finds an element by id in this subtree (this container included).</summary>
    public IText? FindById(int id) => SelfAndDescendants().FirstOrDefault(node => node.Id == id);

    /// <summary>Finds the container that directly holds the element with the given id.</summary>
    public Container? FindParentOf(int id) =>
        SelfAndDescendants()
            .OfType<Container>()
            .FirstOrDefault(container => container.Children.Any(child => child.Id == id));

    /// <inheritdoc />
    public abstract T Accept<T>(ITextVisitor<T> visitor);
}