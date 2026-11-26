using System.Collections.Generic;
using Infra.Data;

namespace OOPproject.SOLID;

/// <summary>The commands about storing data: <c>autosave</c> and <c>storage</c>.</summary>
public sealed class PersistenceStrategy : CommandStrategyBase
{
    private readonly AutoSaver _autoSaver;
    private readonly SaveManager _saves;
    private readonly IPresenter _ui;
    private readonly ImageCache? _images;

    public PersistenceStrategy(AutoSaver autoSaver, SaveManager saves, IPresenter ui, ImageCache? images = null)
    {
        _autoSaver = autoSaver;
        _saves = saves;
        _ui = ui;
        _images = images;

        Register("autosave", HandleAutoSave);
        Register("storage", HandleStorage);
    }

    // autosave [on|off]
    private void HandleAutoSave(string[] args, Dictionary<string, string> opts)
    {
        if (args.Length == 0)
        {
            _ui.Show($"Autosave is {State()} (slot '{AutoSaver.SlotName}').");
            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "on":
                _autoSaver.Enabled = true;
                _ui.Show("Autosave is on.");
                break;
            case "off":
                _autoSaver.Enabled = false;
                _ui.Show("Autosave is off.");
                break;
            default:
                _ui.Show("Usage: autosave [on|off]");
                break;
        }
    }

    // storage
    private void HandleStorage(string[] args, Dictionary<string, string> opts)
    {
        _ui.Show("=== STORAGE ===");
        _ui.Show($"  Saves file:  {_saves.Location}");
        _ui.Show($"  Saved games: {_saves.ListCharacterSaves().Count}, documents: {_saves.ListTextSaves().Count}, battle results: {_saves.ListBattleResults().Count}");
        _ui.Show($"  Last save:   {_saves.LastCharacterSaveName ?? "(none)"}");
        _ui.Show($"  Autosave:    {State()} (slot '{AutoSaver.SlotName}')");

        if (_images != null)
        {
            _ui.Show($"  Pictures:    {_images.List().Count} file(s), {_images.TotalBytes} bytes in {_images.Folder}");
        }
    }

    private string State() => _autoSaver.Enabled ? "on" : "off";
}