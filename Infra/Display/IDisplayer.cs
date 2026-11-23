using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infra.Display
{
    /// <summary>Abstraction over any output channel (console, file, test buffer...).</summary>
    public interface IDisplayer
    {
        void Display(string line);
        void Display(IDisplayable displayable);
        void Write(string text); // no line break, used for prompts
    }

    public class ConsoleDisplayer : IDisplayer
    {
        public void Display(string line) => Console.WriteLine(line);

        public void Display(IDisplayable displayable) => Console.WriteLine(displayable.Render());

        public void Write(string text) => Console.Write(text);
    }
}