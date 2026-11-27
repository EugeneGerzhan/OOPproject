using System;
using System.Windows;
using Infra.Display;

namespace OOPproject.Gui;

/// <summary>
/// The input channel of the window: a question of a command becomes a dialog. A y/n question is a message box,
/// anything else is a small window with a text field. The prompt is the text that was written just before.
/// Cancel stops the command (the command line shows that the input was cancelled).
/// </summary>
public sealed class DialogInputReader : IInputReader
{
    private readonly NotifyingDisplayer _displayer;

    public DialogInputReader(NotifyingDisplayer displayer)
    {
        _displayer = displayer;
    }

    /// <summary>Commands run on a worker thread, so the dialog is opened on the UI thread and the worker waits.</summary>
    public string? ReadLine()
    {
        string prompt = _displayer.PendingPrompt;
        string? answer = Application.Current.Dispatcher.Invoke(() => Ask(prompt));

        if (answer == null)
        {
            throw new OperationCanceledException("The input was cancelled.");
        }

        _displayer.Display(answer); // the answer is echoed like text typed in a console
        return answer;
    }

    private static string? Ask(string prompt)
    {
        Window? owner = Application.Current.MainWindow;

        if (PromptText.IsConfirmation(prompt))
        {
            MessageBoxResult result = MessageBox.Show(
                owner, PromptText.Question(prompt), "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes ? "y" : "n";
        }

        var dialog = new InputDialog(prompt) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Answer : null;
    }
}