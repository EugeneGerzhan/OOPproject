using System;
using System.Collections.Generic;
using System.Linq;
using GameSystem.Core.Abilities;
using GameSystem.Core.Interfaces;
using GameSystem.Core.Mapping;
using GameSystem.Core.Models;
using GameSystem.Engine;
using Infra.Data;
using Infra.Display;

namespace OOPproject.SOLID;

/// <summary>
/// The commands of the text game: choose the characters, start a battle, act in the main loop,
/// look at the history and the results, save and load the game. The rules are in <see cref="Battle"/>;
/// this strategy only translates the commands of the player and shows the data through renderers.
/// </summary>
public sealed class GameStrategy : CommandStrategyBase
{
    private const string Usage = "Usage: game <roster|start|status|history|attack|defend|heal|ability|stop|save|load|results> [...]";
    private const string TurnHint = "Use: game attack <target> | defend | heal [<target>] [--amount <hp>] | ability <target> [--id <ability>] | stop";
    private const int DefaultHealAmount = 15;

    private readonly GameSession _session;
    private readonly SaveManager _saves;
    private readonly IPresenter _ui;
    private readonly IRenderableDisplay _display;
    private readonly IGameLogger _logger;

    private readonly IRenderer<RosterView> _rosterRenderer = new RosterRenderer();
    private readonly IRenderer<BattleBoard> _boardRenderer = new BattleBoardRenderer();
    private readonly IRenderer<IReadOnlyList<BattleResultData>> _resultsRenderer = new ResultsRenderer();
    private readonly IRenderer<IReadOnlyList<TurnRecord>> _historyRenderer = new TurnHistoryRenderer();

    private Battle? _battle;
    private GameState? _battleState; // the state the battle was started in
    private IReadOnlyList<TurnRecord>? _lastHistory; // the history of the last battle that ended or was abandoned

    public GameStrategy(GameSession session, SaveManager saves, IPresenter ui, IRenderableDisplay display, IGameLogger logger)
    {
        _session = session;
        _saves = saves;
        _ui = ui;
        _display = display;
        _logger = logger;

        Register("game", HandleGame);
    }

    // game <roster|start|status|history|attack|defend|heal|ability|stop|save|load|results> ...
    private void HandleGame(string[] args, Dictionary<string, string> opts)
    {
        if (args.Length == 0)
        {
            _ui.Show(Usage);
            return;
        }

        try
        {
            string[] rest = args.Skip(1).ToArray();
            switch (args[0].ToLowerInvariant())
            {
                case "roster": ShowRoster(); break;
                case "start": Start(rest); break;
                case "status": ShowStatus(); break;
                case "history": ShowHistory(); break;
                case "attack": Attack(rest); break;
                case "defend": Defend(); break;
                case "heal": Heal(rest, opts); break;
                case "ability": UseAbility(rest, opts); break;
                case "stop": Stop(); break;
                case "save": SaveGame(rest); break;
                case "load": LoadGame(rest); break;
                case "results": ShowResults(); break;
                default: _ui.Show(Usage); break;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
        {
            // Breaking a rule of the game is not a failure of the program: tell the player what is wrong.
            _ui.Show(ex.Message);
        }
    }

    private void ShowRoster()
    {
        _display.Display(new Renderable<RosterView>(RosterView.From(_session.State), _rosterRenderer));
    }

    private void Start(string[] names)
    {
        if (_battle != null && ReferenceEquals(_battleState, _session.State))
        {
            throw new InvalidOperationException("A battle is already in progress. Use 'game stop' to abandon it.");
        }
        if (names.Length < 2)
        {
            _ui.Show("Usage: game start <name|id> <name|id> [...]");
            return;
        }

        var participants = names.Select(Resolve).ToList();
        var battle = new Battle(participants, _logger); // checks the participants

        _battle = battle;
        _battleState = _session.State;
        _lastHistory = null;

        _ui.Show($"Battle started: {string.Join(" vs ", participants.Select(p => p.Name))}.");
        ShowBoard(battle);
        _ui.Show($"It is {battle.Current.Name}'s turn. {TurnHint}");
    }

    private void ShowStatus()
    {
        Battle battle = Active();
        ShowBoard(battle);
        _ui.Show($"It is {battle.Current.Name}'s turn.");
    }

    private void ShowHistory()
    {
        IReadOnlyList<TurnRecord>? history =
            _battle != null && ReferenceEquals(_battleState, _session.State) ? _battle.History : _lastHistory;

        if (history == null || history.Count == 0)
        {
            _ui.Show("No battle history yet.");
            return;
        }

        _display.Display(new Renderable<IReadOnlyList<TurnRecord>>(history, _historyRenderer));
    }

    private void Attack(string[] rest)
    {
        Battle battle = Active();
        if (rest.Length == 0)
        {
            _ui.Show("Usage: game attack <target>");
            return;
        }

        battle.Attack(Resolve(string.Join(" ", rest)));
        AfterAction(battle);
    }

    private void Defend()
    {
        Battle battle = Active();
        battle.Defend();
        AfterAction(battle);
    }

    private void Heal(string[] rest, Dictionary<string, string> opts)
    {
        Battle battle = Active();

        int amount = DefaultHealAmount;
        if (opts.TryGetValue("--amount", out var amountText) && (!int.TryParse(amountText, out amount) || amount < 1))
        {
            _ui.Show("--amount must be a positive integer.");
            return;
        }

        Character target = rest.Length == 0 ? battle.Current : Resolve(string.Join(" ", rest));
        battle.Heal(target, amount);
        AfterAction(battle);
    }

    private void UseAbility(string[] rest, Dictionary<string, string> opts)
    {
        Battle battle = Active();
        if (rest.Length == 0)
        {
            _ui.Show("Usage: game ability <target> [--id <ability>]");
            return;
        }

        Character target = Resolve(string.Join(" ", rest));

        IAbility? ability = null;
        if (opts.TryGetValue("--id", out var abilityKey)
            && !_session.State.Abilities.TryFind(abilityKey, out ability, out var error))
        {
            throw new InvalidOperationException($"Ability: {error}");
        }

        battle.UseAbility(target, ability);
        AfterAction(battle);
    }

    private void Stop()
    {
        if (_battle == null)
        {
            _ui.Show("No battle in progress.");
            return;
        }

        _lastHistory = _battle.History.ToList();
        _battle = null;
        _ui.Show("Battle abandoned (no result is saved).");
    }

    private void ShowResults()
    {
        _display.Display(new Renderable<IReadOnlyList<BattleResultData>>(_saves.ListBattleResults(), _resultsRenderer));
    }

    // game save <name>: the characters and, when there is one, the battle in progress
    private void SaveGame(string[] rest)
    {
        string name = string.Join(" ", rest).Trim();
        if (name.Length == 0)
        {
            _ui.Show("Usage: game save <name>");
            return;
        }

        CharacterSave save = CharacterSaveMapper.ToSave(name, _session.State);

        Battle? running = _battle != null && ReferenceEquals(_battleState, _session.State) ? _battle : null;
        if (running != null)
        {
            save.Battle = BattleSaveMapper.ToData(running, _session.State);
        }

        _saves.SaveCharacters(save);
        _ui.Show($"Game saved as '{name}' ({_saves.Location}). Battle in progress: {(running != null ? "yes" : "no")}.");
    }

    // game load <name>: replaces the characters and the battle
    private void LoadGame(string[] rest)
    {
        string name = string.Join(" ", rest).Trim();
        if (name.Length == 0)
        {
            _ui.Show("Usage: game load <name>");
            return;
        }

        if (!_saves.TryLoadCharacters(name, out var save))
        {
            _ui.Show($"Save '{name}' not found. Use 'saves' to list available saves.");
            return;
        }

        // Everything is built first, so a damaged save changes nothing.
        GameState state = CharacterSaveMapper.ToGameState(save);
        Battle? battle = save.Battle == null ? null : BattleSaveMapper.ToBattle(save.Battle, state, _logger);

        if (!_ui.Confirm("The current state will be replaced. Continue? (y/n): "))
        {
            _ui.Show("Loading cancelled.");
            return;
        }

        _session.State = state;
        _battle = battle;
        _battleState = battle != null ? state : null;
        _lastHistory = null;

        string battleText = battle == null
            ? "no battle in progress"
            : $"battle restored (round {battle.Round}, turn of {battle.Current.Name})";
        _ui.Show($"Game '{save.SaveName}' loaded: {state.Characters.Count} character(s), {battleText}.");
    }

    // ---------- helpers ----------

    /// <summary>The running battle; throws when there is none or when the game state was replaced by a load.</summary>
    private Battle Active()
    {
        if (_battle == null || _battle.IsOver)
        {
            throw new InvalidOperationException("No battle in progress. Use 'game start <name|id> <name|id> ...'.");
        }
        if (!ReferenceEquals(_battleState, _session.State))
        {
            _battle = null;
            throw new InvalidOperationException("The game state was replaced (load), so the battle was abandoned.");
        }
        return _battle;
    }

    private Character Resolve(string key)
    {
        if (!_session.State.Characters.TryFind(key, out var character, out var error))
        {
            throw new InvalidOperationException($"Character: {error}");
        }
        return character;
    }

    private void ShowBoard(Battle battle)
    {
        var view = battle.CreateView();
        view.UseRenderer(_boardRenderer); // the core only gave the data: how it looks is decided here
        _display.Display(view);
    }

    private void AfterAction(Battle battle)
    {
        ShowBoard(battle);

        if (battle.IsOver)
        {
            Finish(battle);
            return;
        }
        _ui.Show($"It is {battle.Current.Name}'s turn.");
    }

    private void Finish(Battle battle)
    {
        BattleOutcome outcome = battle.Outcome!;

        _saves.AddBattleResult(new BattleResultData(
            outcome.Winner, outcome.Participants.ToList(), outcome.Rounds, outcome.Actions, DateTime.Now));

        string summary = outcome.Winner != null ? $"Winner: {outcome.Winner}" : "Draw";
        _ui.Show($"=== {summary} after {outcome.Rounds} round(s), {outcome.Actions} action(s). Result saved. ===");

        _lastHistory = battle.History.ToList();
        _battle = null;
    }
}