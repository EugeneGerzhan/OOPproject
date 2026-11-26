using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using GameSystem.Core.Abilities;
using GameSystem.Core.Dto;
using GameSystem.Core.Interfaces;
using GameSystem.Core.Mapping;
using GameSystem.Core.Models;
using Infra.Data;

namespace OOPproject.SOLID
{
    /// <summary>
    /// Binds terminal commands of the Characters system to the game state.
    /// Objects are addressed by id or by name. All I/O goes through IPresenter.
    /// </summary>
    public class CharactersCliHandler : CommandStrategyBase
    {
        private const int DefaultHealAmount = 15;

        private readonly IGameLogger _logger;
        private readonly IPresenter _ui;
        private readonly SaveManager _saves;
        private readonly GameSession _session;
        private GameState _state => _session.State;

        // To support a new ability type in 'create ability', add an entry here.
        private readonly Dictionary<string, Func<IAbility>> _abilityTypes;

        public CharactersCliHandler(IGameLogger logger, IPresenter ui, SaveManager saves)
            : this(logger, ui, saves, new GameSession())
        {
        }

        public CharactersCliHandler(IGameLogger logger, IPresenter ui, SaveManager saves, GameSession session)
        {
            _logger = logger;
            _ui = ui;
            _saves = saves;
            _session = session;

            _abilityTypes = new Dictionary<string, Func<IAbility>>
            {
                ["rage"] = () => new RageAbility(),
                ["flurry"] = () => new FlurryOfBlowsAbility(),
                ["custom"] = PromptCustomAbility
            };
            RegisterCommands();
        }

        private void RegisterCommands()
        {
            Register("create", HandleCreate);
            Register("add", HandleAdd);
            Register("act", HandleAct);
            Register("ls", HandleLs);
            Register("save", HandleSave);
            Register("load", HandleLoad);
            Register("saves", HandleSaves);
            Register("help", HandleHelp);
        }

        // ======================= create =======================

        private void HandleCreate(string[] args, Dictionary<string, string> opts)
        {
            const string usage = "Usage: create <char|item|ability>";
            if (args.Length == 0)
            {
                _ui.Show(usage);
                return;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "char":
                case "character":
                    CreateCharacter();
                    break;
                case "item":
                    CreateItem();
                    break;
                case "ability":
                    CreateAbility();
                    break;
                default:
                    _ui.Show(usage);
                    break;
            }
        }

        private void CreateCharacter()
        {
            string name = _ui.Prompt("Enter character name: ");
            if (name.Length == 0)
            {
                _ui.Show("Name cannot be empty.");
                return;
            }
            if (_state.Characters.NameExists(name))
            {
                _ui.Show($"Character '{name}' already exists.");
                return;
            }

            string classKey = _ui.PromptChoice("Enter class", CharacterFactory.ClassKeys);
            int? requestedId = PromptFreeId(_state.Characters);

            var character = CharacterFactory.Create(classKey, name);
            int id = _state.Characters.Add(character, requestedId);
            int abilityId = _state.Abilities.Add(character.UniqueAbility);

            _ui.Show($"Character '{name}' ({character.GetType().Name}) created with id {id}.");
            _ui.Show($"Its unique ability '{character.UniqueAbility.Name}' is registered with id {abilityId}.");
        }

        private void CreateItem()
        {
            string name = _ui.Prompt("Enter item name: ");
            if (name.Length == 0)
            {
                _ui.Show("Name cannot be empty.");
                return;
            }

            int health = _ui.PromptInt("Health bonus", 0, 0);
            int armor = _ui.PromptInt("Armor bonus", 0, 0);
            int attack = _ui.PromptInt("Attack bonus", 0, 0);
            int? requestedId = PromptFreeId(_state.Items);

            var item = new Item(name, healthBonus: health, armorBonus: armor, attackBonus: attack);
            int id = _state.Items.Add(item, requestedId);
            _ui.Show($"Item '{name}' created with id {id}.");
        }

        private void CreateAbility()
        {
            string typeKey = _ui.PromptChoice("Enter ability type", _abilityTypes.Keys.ToList());
            IAbility ability = _abilityTypes[typeKey]();
            int? requestedId = PromptFreeId(_state.Abilities);

            int id = _state.Abilities.Add(ability, requestedId);
            _ui.Show($"Ability '{ability.Name}' created with id {id}.");
        }

        private IAbility PromptCustomAbility()
        {
            string name = _ui.Prompt("Enter ability name: ");
            int hits = _ui.PromptInt("Number of attacks", 1, 1);
            int bonus = _ui.PromptInt("Attack bonus while used", 0, 0);
            return new CustomAbility(name, hits, bonus);
        }

        // ======================= add =======================

        // add <item|ability> [--char_id <id/name>] [--id <id/name>]
        private void HandleAdd(string[] args, Dictionary<string, string> opts)
        {
            const string usage = "Usage: add <item|ability> [--char_id <id/name>] [--id <id/name>]";
            if (args.Length == 0)
            {
                _ui.Show(usage);
                return;
            }

            string kind = args[0].ToLowerInvariant();
            if (kind != "item" && kind != "ability")
            {
                _ui.Show(usage);
                return;
            }

            string charKey = GetOptionOrPrompt(opts, "--char_id", "Enter character id/name: ");
            string objectKey = GetOptionOrPrompt(opts, "--id",
                kind == "item" ? "Enter item id/name: " : "Enter ability id/name: ");

            if (!TryResolve(_state.Characters, charKey, "Character", out var character)) return;

            if (kind == "item")
            {
                if (!TryResolve(_state.Items, objectKey, "Item", out var item)) return;
                if (!character.IsAlive)
                {
                    _ui.Show($"{character.Name} is defeated and cannot equip items.");
                    return;
                }
                character.Equip(item, _logger);
            }
            else
            {
                if (!TryResolve(_state.Abilities, objectKey, "Ability", out var ability)) return;
                character.LearnAbility(ability, _logger);
            }
        }

        // ======================= act =======================

        // act <attack|heal|ability> <actor> <target> [--id <ability id/name>] [--amount <hp>]
        private void HandleAct(string[] args, Dictionary<string, string> opts)
        {
            const string usage = "Usage: act <attack|heal|ability> <actor> <target> [--id <ability id/name>] [--amount <hp>]";
            if (args.Length < 3)
            {
                _ui.Show(usage);
                return;
            }

            string action = args[0].ToLowerInvariant();
            if (action != "attack" && action != "heal" && action != "ability")
            {
                _ui.Show($"Unknown action: {action}. {usage}");
                return;
            }

            if (!TryResolve(_state.Characters, args[1], "Actor", out var actor)) return;
            if (!TryResolve(_state.Characters, args[2], "Target", out var target)) return;

            if (!actor.IsAlive)
            {
                _ui.Show($"{actor.Name} is defeated and cannot act.");
                return;
            }

            switch (action)
            {
                case "attack":
                    if (RejectSelfTarget(actor, target)) return;
                    actor.Attack(target, _logger);
                    break;
                case "heal":
                    ActHeal(target, opts);
                    break;
                case "ability":
                    ActAbility(actor, target, opts);
                    break;
            }
        }

        private void ActHeal(Character target, Dictionary<string, string> opts)
        {
            int amount = DefaultHealAmount;
            if (opts.TryGetValue("--amount", out var amountText)
                && (!int.TryParse(amountText, out amount) || amount < 1))
            {
                _ui.Show("--amount must be a positive integer.");
                return;
            }

            if (!target.IsAlive)
            {
                _ui.Show($"{target.Name} is defeated and cannot be healed.");
                return;
            }

            target.Heal(amount, _logger);
        }

        private void ActAbility(Character actor, Character target, Dictionary<string, string> opts)
        {
            if (RejectSelfTarget(actor, target)) return;

            if (!target.IsAlive)
            {
                _ui.Show($"{target.Name} is already defeated.");
                return;
            }

            // No --id: use the character's unique ability.
            if (!opts.TryGetValue("--id", out var abilityKey))
            {
                actor.UseSpecialAbility(target, _logger);
                return;
            }

            if (!TryResolve(_state.Abilities, abilityKey, "Ability", out var ability)) return;

            if (!actor.HasAbility(ability))
            {
                _ui.Show($"{actor.Name} does not know '{ability.Name}'. Teach it first with 'add ability'.");
                return;
            }

            actor.UseAbility(ability, target, _logger);
        }

        private bool RejectSelfTarget(Character actor, Character target)
        {
            if (!ReferenceEquals(actor, target)) return false;
            _ui.Show($"{actor.Name} cannot target itself with this action.");
            return true;
        }

        // ======================= ls =======================

        // ls [<char|item|ability>] [--id <id/name>]
        private void HandleLs(string[] args, Dictionary<string, string> opts)
        {
            const string usage = "Usage: ls [<char|item|ability>] [--id <id/name>]";
            bool hasId = opts.TryGetValue("--id", out var key);
            key ??= string.Empty;

            if (args.Length == 0)
            {
                if (hasId)
                {
                    _ui.Show("Specify a category: ls <char|item|ability> --id <id/name>");
                    return;
                }
                ListCharacters();
                ListItems();
                ListAbilities();
                return;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "char":
                case "chars":
                case "character":
                case "characters":
                    if (hasId) ShowCharacter(key); else ListCharacters();
                    break;
                case "item":
                case "items":
                    if (hasId) ShowItem(key); else ListItems();
                    break;
                case "ability":
                case "abilities":
                    if (hasId) ShowAbility(key); else ListAbilities();
                    break;
                default:
                    _ui.Show(usage);
                    break;
            }
        }

        private void ListCharacters()
        {
            _ui.Show("=== CHARACTERS ===");
            if (_state.Characters.Count == 0)
            {
                _ui.Show("  (none)");
                return;
            }
            foreach (var entry in _state.Characters.Entries)
            {
                _ui.Show(new CharacterDto(_state, entry.Key, entry.Value, false));
            }
        }

        private void ListItems()
        {
            _ui.Show("=== ITEMS ===");
            if (_state.Items.Count == 0)
            {
                _ui.Show("  (none)");
                return;
            }
            foreach (var entry in _state.Items.Entries)
            {
                _ui.Show(new ItemDto(_state, entry.Key, entry.Value, false));
            }
        }

        private void ListAbilities()
        {
            _ui.Show("=== ABILITIES ===");
            if (_state.Abilities.Count == 0)
            {
                _ui.Show("  (none)");
                return;
            }
            foreach (var entry in _state.Abilities.Entries)
            {
                _ui.Show(new AbilityDto(_state, entry.Key, entry.Value, false));
            }
        }

        private void ShowCharacter(string key)
        {
            if (!TryResolve(_state.Characters, key, "Character", out var c)) return;
            _ui.Show(new CharacterDto(_state, _state.Characters.IdOf(c) ?? 0, c, true));
        }

        private void ShowItem(string key)
        {
            if (!TryResolve(_state.Items, key, "Item", out var i)) return;
            _ui.Show(new ItemDto(_state, _state.Items.IdOf(i) ?? 0, i, true));
        }

        private void ShowAbility(string key)
        {
            if (!TryResolve(_state.Abilities, key, "Ability", out var a)) return;
            _ui.Show(new AbilityDto(_state, _state.Abilities.IdOf(a) ?? 0, a, true));
        }

        // ======================= save / load =======================

        // save <name>
        private void HandleSave(string[] args, Dictionary<string, string> opts)
        {
            string name = string.Join(" ", args).Trim();
            if (name.Length == 0)
            {
                _ui.Show("Usage: save <name>");
                return;
            }

            _saves.SaveCharacters(CharacterSaveMapper.ToSave(name, _state));
            _ui.Show($"Game state saved as '{name}' ({_saves.Location}).");
        }

        // load <name>
        private void HandleLoad(string[] args, Dictionary<string, string> opts)
        {
            string name = string.Join(" ", args).Trim();
            if (name.Length == 0)
            {
                _ui.Show("Usage: load <name>");
                return;
            }

            if (!_saves.TryLoadCharacters(name, out var save))
            {
                _ui.Show($"Save '{name}' not found. Use 'saves' to list available saves.");
                return;
            }

            var newState = CharacterSaveMapper.ToGameState(save); // validates the save before anything is replaced

            if (!_ui.Confirm("The current state will be replaced. Continue? (y/n): "))
            {
                _ui.Show("Loading cancelled.");
                return;
            }

            _session.State = newState;
            _ui.Show($"Loaded '{save.SaveName}': {_state.Characters.Count} character(s), {_state.Items.Count} item(s), {_state.Abilities.Count} ability(ies).");
        }

        // saves
        private void HandleSaves(string[] args, Dictionary<string, string> opts)
        {
            var list = _saves.ListCharacterSaves();
            if (list.Count == 0)
            {
                _ui.Show($"No saved game states ({_saves.Location}).");
                return;
            }

            _ui.Show("=== SAVED GAME STATES ===");
            foreach (var info in list)
            {
                _ui.Show($"  {info.Name} ({info.LastModified:yyyy-MM-dd HH:mm})");
            }
        }

        // ======================= help =======================

        private void HandleHelp(string[] args, Dictionary<string, string> opts)
        {
            _ui.Show("Available commands (objects are addressed by id or name):");
            _ui.Show("  create <char|item|ability>                       - create an object via dialog");
            _ui.Show("  add <item|ability> [--char_id <c>] [--id <x>]    - give an item / teach an ability to a character");
            _ui.Show("  act <attack|heal|ability> <actor> <target>       - perform an action; [--id <ability>] picks the ability,");
            _ui.Show("                                                     [--amount <hp>] sets heal amount");
            _ui.Show("  ls [<char|item|ability>] [--id <x>]              - short list, or full state of one object");
            _ui.Show("  save <name> | load <name> | saves                - persist / restore / list game states (JSON file)");
            _ui.Show("  remote <ls|show|import> <char|weapon> [<id>]     - browse / import characters and weapons from the remote database");
            _ui.Show("  sheet <id/name>                                   - structured-text character sheet");
            _ui.Show("  game <roster|start|status|attack|defend|heal|ability|stop|results> - the text game (battle)");
            _ui.Show("  commands                                         - list the commands of all strategies");
            _ui.Show("  game continue                                      - continue from the last save (also offered at start-up)");
            _ui.Show("  autosave [on|off] | storage                        - automatic saving, where the data live");
            _ui.Show("  remote portrait <id> | portraits | cache [clear]   - pictures of characters and their local cache");
            _ui.Show("  exit                                             - quit");
        }

        // ======================= helpers =======================

        private bool TryResolve<T>(Registry<T> registry, string key, string label, [NotNullWhen(true)] out T? result)
            where T : class
        {
            if (registry.TryFind(key, out result, out string error))
            {
                return true;
            }
            _ui.Show($"{label}: {error}");
            return false;
        }

        private string GetOptionOrPrompt(Dictionary<string, string> opts, string option, string prompt)
        {
            if (opts.TryGetValue(option, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
            return _ui.Prompt(prompt);
        }

        private int? PromptFreeId<T>(Registry<T> registry) where T : class
        {
            while (true)
            {
                string input = _ui.Prompt("Enter id (leave empty for automatic): ");
                if (input.Length == 0) return null;
                if (!int.TryParse(input, out int id) || id < 1)
                {
                    _ui.Show("Id must be a positive number.");
                    continue;
                }
                if (!registry.IsFree(id))
                {
                    _ui.Show($"Id {id} is already taken.");
                    continue;
                }
                return id;
            }
        }
    }
}