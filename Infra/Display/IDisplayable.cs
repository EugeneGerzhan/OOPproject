using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infra.Display
{
    /// <summary>An object that knows how to present itself as text (implemented by DTOs).</summary>
    public interface IDisplayable
    {
        string Render();
    }
}
