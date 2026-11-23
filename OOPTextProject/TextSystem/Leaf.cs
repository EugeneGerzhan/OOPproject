using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OOPTextProject.TextSystem;

/// <summary>Abstract leaf class representing terminal text elements without children.</summary>
public abstract class Leaf : IText
{
    protected readonly string _content;

    public int Id { get; }

    public string Name => _content;

    protected Leaf(int id, string content)
    {
        Id = id;
        _content = content;
    }

    /// <inheritdoc />
    public abstract T Accept<T>(ITextVisitor<T> visitor);
}