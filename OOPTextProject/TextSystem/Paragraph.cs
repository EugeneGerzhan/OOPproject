using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;

namespace OOPTextProject.TextSystem;

/// <summary>Paragraph element representing a block of text. Can be copied (Prototype).</summary>
public class Paragraph : Leaf, IPrototype<IText>
{
    public Paragraph(int id, string content) : base(id, content) { }

    public string Content => _content;

    /// <inheritdoc />
    public IText Clone(Func<int> nextId) => new Paragraph(nextId(), _content);

    /// <inheritdoc />
    public override T Accept<T>(ITextVisitor<T> visitor) => visitor.VisitParagraph(this);
}