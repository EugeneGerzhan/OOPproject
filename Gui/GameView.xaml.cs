using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Infra.Data;

namespace OOPproject.Gui;

/// <summary>
/// Theme 2: the window as a console of the text game. Everything the commands print appears in the output field,
/// a command is typed in the input field (Enter), and the buttons choose and load saves or run frequent commands.
/// </summary>
public partial class GameView : UserControl
{
    private const int MaxOutputLength = 300_000;

    private readonly List<string> _history = new();
    private AppServices _services = null!;
    private int _historyIndex;
    private int _running;

    public GameView()
    {
        InitializeComponent();
    }

    public void Attach(AppServices services)
    {
        _services = services;

        // The commands run on a worker thread and print through this event.
        _services.Displayer.TextWritten += OnTextWritten;

        DataFolderText.Text = $"Data folder: {services.Paths.DataDirectory}";
        Append($"=== Text game ==={Environment.NewLine}Type a command (try 'help'), or use the buttons.{Environment.NewLine}");
        RefreshSaves();
    }

    /// <summary>Echoes a command line, runs it and refreshes the list of saves. Safe to call from other views.</summary>
    public async Task SubmitAsync(string commandLine)
    {
        string line = commandLine.Trim();
        if (line.Length == 0)
        {
            return;
        }

        _history.Add(line);
        _historyIndex = _history.Count;

        if (line.Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            Window.GetWindow(this)?.Close();
            return;
        }

        Append($"> {line}{Environment.NewLine}");
        SetBusy(true);
        try
        {
            await _services.Runner.RunAsync(line);
        }
        finally
        {
            SetBusy(false);
            RefreshSaves();
            InputBox.Focus();
        }
    }

    /// <summary>At the start: offers to continue from the last save (an autosave counts too).</summary>
    public async Task OfferToContinueAsync()
    {
        string? last;
        try
        {
            last = _services.Saves.LastCharacterSaveName;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        {
            Append($"The saves file could not be read: {ex.Message}{Environment.NewLine}");
            return;
        }

        if (last == null)
        {
            return;
        }

        MessageBoxResult answer = MessageBox.Show(
            Window.GetWindow(this), $"The last save is '{last}'.\nContinue from it?",
            "Continue the game", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        SetBusy(true);
        try
        {
            await _services.Runner.RunAsync(() => _services.Game.ContinueFromLastSave(askConfirmation: false));
        }
        catch (Exception ex)
        {
            Append($"The last save could not be loaded: {ex.Message}{Environment.NewLine}");
        }
        finally
        {
            SetBusy(false);
            RefreshSaves();
        }
    }

    // ---------- events of the controls ----------

    private async void Send_Click(object sender, RoutedEventArgs e) => await SubmitInputAsync();

    private async void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                await SubmitInputAsync();
                break;
            case Key.Up:
                e.Handled = true;
                if (_historyIndex > 0)
                {
                    _historyIndex--;
                    ShowHistoryItem();
                }
                break;
            case Key.Down:
                e.Handled = true;
                if (_historyIndex < _history.Count)
                {
                    _historyIndex++;
                    ShowHistoryItem();
                }
                break;
        }
    }

    private async void QuickCommand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string command })
        {
            await SubmitAsync(command);
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => OutputBox.Clear();

    private async void Load_Click(object sender, RoutedEventArgs e) => await LoadSelectedAsync();

    private async void SavesList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => await LoadSelectedAsync();

    private async void Continue_Click(object sender, RoutedEventArgs e) => await SubmitAsync("game continue");

    private async void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new InputDialog("Name of the save:", "Save the game", "save1") { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
        {
            string name = Unquoted(dialog.Answer);
            if (name.Length > 0)
            {
                await SubmitAsync($"game save \"{name}\"");
            }
        }
    }

    private void RefreshSaves_Click(object sender, RoutedEventArgs e) => RefreshSaves();

    private void SavesList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    // ---------- helpers ----------

    private async Task LoadSelectedAsync()
    {
        if (SavesList.SelectedItem is SaveInfo save)
        {
            await SubmitAsync($"game load \"{Unquoted(save.Name)}\"");
        }
    }

    private async Task SubmitInputAsync()
    {
        string text = InputBox.Text;
        InputBox.Clear();
        await SubmitAsync(text);
    }

    private void ShowHistoryItem()
    {
        InputBox.Text = _historyIndex < _history.Count ? _history[_historyIndex] : string.Empty;
        InputBox.CaretIndex = InputBox.Text.Length;
    }

    private void OnTextWritten(string text)
    {
        // Invoke (not BeginInvoke): the order of the text and of the dialogs of the questions stays as it was printed.
        Dispatcher.Invoke(() => Append(text));
    }

    private void Append(string text)
    {
        OutputBox.AppendText(text);
        if (OutputBox.Text.Length > MaxOutputLength)
        {
            OutputBox.Text = OutputBox.Text.Substring(OutputBox.Text.Length - MaxOutputLength / 2);
        }
        OutputBox.ScrollToEnd();
    }

    private void RefreshSaves()
    {
        try
        {
            SavesList.ItemsSource = _services.Saves.ListCharacterSaves();
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        {
            Append($"The saves could not be listed: {ex.Message}{Environment.NewLine}");
        }
    }

    private void SetBusy(bool busy)
    {
        _running += busy ? 1 : -1;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool idle = _running <= 0;
        InputBox.IsEnabled = idle;
        SendButton.IsEnabled = idle;
        ContinueButton.IsEnabled = idle;
        SaveButton.IsEnabled = idle;
        LoadButton.IsEnabled = idle && SavesList.SelectedItem != null;
    }

    /// <summary>Names are put in quotes in a command, so a quote inside a name is dropped.</summary>
    private static string Unquoted(string name) => name.Replace("\"", string.Empty).Trim();
}