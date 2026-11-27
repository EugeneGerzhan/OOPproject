using System;

namespace Infra.Display
{
    /// <summary>
    /// An output channel that reports everything written to it by an event. A window subscribes to the event and puts
    /// the text into a field, so the same commands that print to the console work in a graphical interface.
    /// It also remembers the last text written without a line break: that is the prompt of the next input.
    /// </summary>
    public sealed class NotifyingDisplayer : IDisplayer
    {
        /// <summary>Raised for every piece of text (it can be raised on any thread).</summary>
        public event Action<string>? TextWritten;

        /// <summary>The text that was written without a line break and not yet answered (empty when there is none).</summary>
        public string PendingPrompt { get; private set; } = string.Empty;

        public void Display(string line)
        {
            PendingPrompt = string.Empty;
            TextWritten?.Invoke(line + Environment.NewLine);
        }

        public void Display(IDisplayable displayable) => Display(displayable.Render());

        public void Write(string text)
        {
            PendingPrompt = text;
            TextWritten?.Invoke(text);
        }
    }

    /// <summary>Tells what kind of an input a prompt asks for.</summary>
    public static class PromptText
    {
        private static readonly System.Text.RegularExpressions.Regex YesNo =
            new System.Text.RegularExpressions.Regex(@"\s*\(y/n\)\s*:?\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>True for a question that is answered with y or n, for example "Continue? (y/n): ".</summary>
        public static bool IsConfirmation(string prompt) => YesNo.IsMatch(prompt ?? string.Empty);

        /// <summary>The question without the "(y/n):" ending.</summary>
        public static string Question(string prompt) => YesNo.Replace(prompt ?? string.Empty, string.Empty).Trim();
    }
}