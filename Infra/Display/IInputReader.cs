using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infra.Display
{
    /// <summary>Abstraction over any input channel.</summary>
    public interface IInputReader
    {
        string? ReadLine();
    }

    public class ConsoleInputReader : IInputReader
    {
        public string? ReadLine() => Console.ReadLine();
    }
}