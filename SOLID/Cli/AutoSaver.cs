using System;
using System.Linq;
using System.Text.Json;
using GameSystem.Core.Mapping;
using GameSystem.Core.Models;
using GameSystem.Engine;
using Infra.Data;

namespace OOPproject.SOLID;

/// <summary>
/// Saves the game by itself: after a command, if the game has changed, its whole state (the characters, the items,
/// the abilities and the battle in progress) is written to the slot <c>autosave</c>. The next start of the program can
/// continue from it.
/// </summary>
/// <remarks>
/// A change is found by comparing a fingerprint of the state (without the time), so a command that changed nothing
/// writes nothing. The first write of a session keeps the autosave of the last session as <c>autosave-previous</c>:
/// a player who declined to continue and started a new game does not lose the old one.
/// </remarks>
public sealed class AutoSaver
{
    public const string SlotName = "autosave";
    public const string PreviousSlotName = "autosave-previous";

    private readonly GameSession _session;
    private readonly SaveManager _saves;
    private readonly Func<Battle?> _currentBattle;

    private string? _lastFingerprint;
    private bool _rotated;

    /// <param name="currentBattle">Gives the battle that is running now (null when there is none).</param>
    public AutoSaver(GameSession session, SaveManager saves, Func<Battle?> currentBattle)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _saves = saves ?? throw new ArgumentNullException(nameof(saves));
        _currentBattle = currentBattle ?? throw new ArgumentNullException(nameof(currentBattle));
    }

    public bool Enabled { get; set; } = true;

    /// <returns>True when the autosave was written.</returns>
    public bool SaveIfChanged()
    {
        if (!Enabled || IsEmpty(_session.State))
        {
            return false;
        }

        // The first time, the autosave that is already there counts as "the last saved state".
        if (_lastFingerprint == null && _saves.TryLoadCharacters(SlotName, out var existing))
        {
            _lastFingerprint = Fingerprint(existing);
        }

        CharacterSave save = CharacterSaveMapper.ToSave(SlotName, _session.State);
        Battle? battle = _currentBattle();
        if (battle != null && !battle.IsOver)
        {
            save.Battle = BattleSaveMapper.ToData(battle, _session.State);
        }

        string fingerprint = Fingerprint(save);
        if (fingerprint == _lastFingerprint)
        {
            return false;
        }

        KeepPreviousAutosave();
        _saves.SaveCharacters(save);
        _lastFingerprint = fingerprint;
        return true;
    }

    private void KeepPreviousAutosave()
    {
        if (_rotated)
        {
            return;
        }
        _rotated = true;

        if (_saves.TryLoadCharacters(SlotName, out var old))
        {
            _saves.SaveCharacters(new CharacterSave
            {
                SaveName = PreviousSlotName,
                LastModified = old.LastModified,
                Characters = old.Characters.ToList(),
                Abilities = old.Abilities.ToList(),
                Items = old.Items.ToList(),
                ItemLinks = old.ItemLinks.ToList(),
                AbilityLinks = old.AbilityLinks.ToList(),
                Battle = old.Battle
            });
        }
    }

    private static bool IsEmpty(GameState state) =>
        state.Characters.Count == 0 && state.Items.Count == 0 && state.Abilities.Count == 0;

    private static string Fingerprint(CharacterSave save) =>
        JsonSerializer.Serialize(new
        {
            save.Characters,
            save.Abilities,
            save.Items,
            save.ItemLinks,
            save.AbilityLinks,
            save.Battle
        });
}