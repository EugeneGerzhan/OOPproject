using System;
using System.Collections.Generic;
using System.Linq;
using Infra.Display;

namespace OOPproject.SOLID
{
    /// <summary>The only way command handlers talk to the user (input and output).</summary>
    public interface IPresenter
    {
        void Show(string line);
        void Show(IDisplayable displayable);
        string Prompt(string text);
        bool Confirm(string question);
        int PromptInt(string label, int defaultValue, int min);
        string PromptChoice(string label, IReadOnlyList<string> options);
    }

    public class Presenter : IPresenter
    {
        private readonly IDisplayer _displayer;
        private readonly IInputReader _input;

        public Presenter(IDisplayer displayer, IInputReader input)
        {
            _displayer = displayer;
            _input = input;
        }

        public void Show(string line) => _displayer.Display(line);

        public void Show(IDisplayable displayable) => _displayer.Display(displayable);

        /// <summary>Writes the prompt and returns the trimmed answer (never null).</summary>
        public string Prompt(string text)
        {
            _displayer.Write(text);
            return (_input.ReadLine() ?? string.Empty).Trim();
        }

        public bool Confirm(string question)
        {
            string answer = Prompt(question);
            return string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase)
                || string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
        }

        public int PromptInt(string label, int defaultValue, int min)
        {
            while (true)
            {
                string input = Prompt($"{label} [{defaultValue}]: ");
                if (input.Length == 0) return defaultValue;
                if (int.TryParse(input, out int value) && value >= min) return value;
                Show($"Enter an integer >= {min}.");
            }
        }

        public string PromptChoice(string label, IReadOnlyList<string> options)
        {
            while (true)
            {
                string input = Prompt($"{label} ({string.Join("/", options)}) [{options[0]}]: ").ToLowerInvariant();
                if (input.Length == 0) return options[0];
                if (options.Contains(input)) return input;
                Show($"Choose one of: {string.Join(", ", options)}.");
            }
        }
    }
}