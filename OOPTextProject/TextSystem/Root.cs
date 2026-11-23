using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OOPTextProject.TextSystem;

/// <summary>Root container representing the whole document. Its id is always 0.</summary>
public class Root : Container
{
    public Root(string title) : base(0, title) { }

    /// <inheritdoc />
    public override T Accept<T>(ITextVisitor<T> visitor) => visitor.VisitRoot(this);
}
