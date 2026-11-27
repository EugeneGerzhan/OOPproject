using System.Windows;

namespace OOPproject.Gui;

public partial class InputDialog : Window
{
    public InputDialog(string prompt, string title = "Input", string defaultAnswer = "")
    {
        InitializeComponent();

        Title = title;
        PromptText.Text = string.IsNullOrWhiteSpace(prompt) ? "Input:" : prompt.Trim();
        AnswerBox.Text = defaultAnswer;
        Loaded += (_, _) =>
        {
            AnswerBox.Focus();
            AnswerBox.SelectAll();
        };
    }

    public string Answer { get; private set; } = string.Empty;

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Answer = AnswerBox.Text;
        DialogResult = true;
    }
}