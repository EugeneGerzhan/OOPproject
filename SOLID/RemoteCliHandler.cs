using GameSystem.Core.Models;
using GameSystem.Core.Remote;
using Infra.Data;
using Infra.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OOPproject.SOLID
{
    /// <summary>Commands that work with the remote database: remote ls/show/import and sheet.</summary>
    public class RemoteCliHandler : CommandStrategyBase
    {
        private const string CharKind = "char";
        private const string WeaponKind = "weapon";
        private const int DefaultLimit = 40;
        private const int Columns = 3;
        private const int ColumnWidth = 24;

        private readonly IGameDataApi _api;
        private readonly RemoteImporter _importer;
        private readonly GameSession _session;
        private readonly IPresenter _ui;
        private readonly CharacterSheetBuilder _sheets;
        private readonly ImageCache? _images;

        public RemoteCliHandler(IGameDataApi api, RemoteImporter importer, GameSession session,
                        IPresenter ui, CharacterSheetBuilder sheets, ImageCache? images = null)
        {
            _api = api;
            _importer = importer;
            _session = session;
            _ui = ui;
            _sheets = sheets;
            _images = images;

            RegisterCommands();
        }

        private void RegisterCommands()
        {
            Register("remote", HandleRemote);
            Register("sheet", HandleSheet);
        }

        // remote <ls|show|import> <char|weapon> [<id>] [--find <text>] [--limit <n>]
        private void HandleRemote(string[] args, Dictionary<string, string> opts)
        {
            const string usage = "Usage: remote <ls|show|import> <char|weapon> [<id>] [--find <text>] [--limit <n>]";
            if (args.Length > 0)
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "portrait":
                        ShowPortrait(string.Join(" ", args.Skip(1)).Trim());
                        return;
                    case "portraits":
                        DownloadPortraits(opts);
                        return;
                    case "cache":
                        HandleCache(args.Skip(1).ToArray());
                        return;
                }
            }
            if (args.Length < 2)
            {
                _ui.Show(usage);
                return;
            }

            string action = args[0].ToLowerInvariant();
            string? kind = NormalizeKind(args[1]);
            if (kind == null)
            {
                _ui.Show(usage);
                return;
            }

            string id = string.Join(" ", args.Skip(2)).Trim();

            switch (action)
            {
                case "ls":
                    ListRemote(kind, opts);
                    break;
                case "show":
                case "import":
                    if (id.Length == 0)
                    {
                        _ui.Show($"Usage: remote {action} <char|weapon> <id>");
                        return;
                    }
                    if (action == "show") ShowRemote(kind, id); else ImportRemote(kind, id);
                    break;
                default:
                    _ui.Show(usage);
                    break;
            }
        }

        private void ListRemote(string kind, Dictionary<string, string> opts)
        {
            IReadOnlyList<string> ids = kind == CharKind
                ? Wait(_api.GetCharacterIdsAsync())
                : Wait(_api.GetWeaponIdsAsync());
            string title = kind == CharKind ? "CHARACTERS" : "WEAPONS";

            if (opts.TryGetValue("--find", out var find) && !string.IsNullOrWhiteSpace(find))
            {
                string needle = find.Trim().ToLowerInvariant().Replace(' ', '-');
                ids = ids.Where(i => i.Contains(needle, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            int limit = DefaultLimit;
            if (opts.TryGetValue("--limit", out var limitText) && !string.IsNullOrWhiteSpace(limitText)
                && (!int.TryParse(limitText, out limit) || limit < 1))
            {
                _ui.Show("--limit must be a positive integer.");
                return;
            }

            _ui.Show($"=== REMOTE {title} ({ids.Count}) ===");
            if (ids.Count == 0)
            {
                _ui.Show("  (nothing found)");
                return;
            }

            var shown = ids.Take(limit).ToList();
            for (int i = 0; i < shown.Count; i += Columns)
            {
                string row = string.Concat(shown.Skip(i).Take(Columns).Select(id => id.PadRight(ColumnWidth)));
                _ui.Show("  " + row.TrimEnd());
            }

            if (ids.Count > shown.Count)
            {
                _ui.Show($"  ... and {ids.Count - shown.Count} more (use --find <text> or --limit <n>)");
            }
        }

        private void ShowRemote(string kind, string id)
        {
            if (kind == CharKind)
            {
                _ui.Show(DescribeCharacter(Wait(_api.GetCharacterAsync(id))));
            }
            else
            {
                _ui.Show(DescribeWeapon(Wait(_api.GetWeaponAsync(id))));
            }
        }

        private void ImportRemote(string kind, string id)
        {
            if (kind == CharKind)
            {
                var remote = Wait(_api.GetCharacterAsync(id));
                var result = _importer.ImportCharacter(remote, _session.State);
                var c = result.Character;

                var learned = result.LearnedAbilityIds
                    .Zip(c.LearnedAbilities, (abilityId, ability) => $"[{abilityId}] {ability.Name}")
                    .ToList();

                _ui.Show($"Imported character '{c.Name}' with id {result.CharacterId} (rarity {remote.Rarity}).");
                _ui.Show($"  Stats: HP {c.MaxHealth}, ARM {c.Armor}, ATK {c.AttackPower}");
                _ui.Show($"  Unique ability: [{result.UniqueAbilityId}] {c.UniqueAbility.Name}");
                _ui.Show($"  Learned abilities: {(learned.Count == 0 ? "none" : string.Join(", ", learned))}");
            }
            else
            {
                var remote = Wait(_api.GetWeaponAsync(id));
                var result = _importer.ImportWeapon(remote, _session.State);
                var item = result.Item;

                _ui.Show($"Imported weapon '{item.Name}' as item with id {result.ItemId} (+{item.AttackBonus} ATK, +{item.ArmorBonus} ARM, +{item.HealthBonus} HP).");
                _ui.Show($"  Equip it with: add item --char_id <id/name> --id {result.ItemId}");
            }
        }

        // sheet <id/name> [--format plain|html|markdown]
        private void HandleSheet(string[] args, Dictionary<string, string> opts)
        {
            string key = string.Join(" ", args).Trim();
            if (key.Length == 0)
            {
                _ui.Show("Usage: sheet <id/name>");
                return;
            }

            if (!FormatOption.TryResolve(opts, _ui, out var formats))
            {
                return;
            }

            if (!_session.State.Characters.TryFind(key, out var character, out var error))
            {
                _ui.Show($"Character: {error}");
                return;
            }

            _ui.Show(_sheets.Build(character, formats));
        }

        // ---------- helpers ----------

        private static string? NormalizeKind(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "char":
                case "chars":
                case "character":
                case "characters":
                    return CharKind;
                case "weapon":
                case "weapons":
                    return WeaponKind;
                default:
                    return null;
            }
        }

        private static string DescribeCharacter(RemoteCharacter c)
        {
            var lines = new List<string>
            {
                $"=== REMOTE CHARACTER: {c.Name} ({c.Id}) ===",
                $"  Title:       {c.Title}",
                $"  Vision:      {c.Vision}",
                $"  Weapon type: {c.WeaponType}",
                $"  Rarity:      {c.Rarity}",
                $"  Nation:      {c.Nation}",
                $"  Affiliation: {c.Affiliation}",
                $"  About:       {CharacterSheetBuilder.Shorten(c.Description, 160)}",
                $"  Image:       {(string.IsNullOrWhiteSpace(c.ImageUrl) ? "-" : c.ImageUrl)}",
                "  Talents:"
            };
            lines.AddRange(c.SkillTalents.Select(t => $"    - {t.Name} ({t.Unlock})"));
            return string.Join(Environment.NewLine, lines);
        }

        private static string DescribeWeapon(RemoteWeapon w)
        {
            return string.Join(Environment.NewLine, new[]
            {
                $"=== REMOTE WEAPON: {w.Name} ({w.Id}) ===",
                $"  Type:        {w.Type}",
                $"  Rarity:      {w.Rarity}",
                $"  Base attack: {w.BaseAttack}",
                $"  Sub stat:    {w.SubStat}",
                $"  Passive:     {w.PassiveName}",
                $"  Location:    {w.Location}"
            });
        }

        /// <summary>Console app: no synchronization context, so blocking on a task is safe here.</summary>
        private static T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();
        // remote portrait <id>
        private void ShowPortrait(string id)
        {
            if (!RequireImages())
            {
                return;
            }
            if (id.Length == 0)
            {
                _ui.Show("Usage: remote portrait <id>");
                return;
            }

            var remote = Wait(_api.GetCharacterAsync(id));
            CachedImage image = DownloadPortrait(remote);

            string source = image.FromCache
                ? (image.Stale ? "from the cache (expired, the service did not answer)" : "from the cache")
                : "downloaded";
            _ui.Show($"Portrait of {remote.Name}: {image.FilePath} ({image.Size} bytes, {source}).");
        }

        // remote portraits [--find <text>] [--limit <n>]: puts many portraits into the cache
        private void DownloadPortraits(Dictionary<string, string> opts)
        {
            if (!RequireImages())
            {
                return;
            }

            IReadOnlyList<string> ids = Wait(_api.GetCharacterIdsAsync());
            if (opts.TryGetValue("--find", out var find) && !string.IsNullOrWhiteSpace(find))
            {
                string needle = find.Trim().ToLowerInvariant().Replace(' ', '-');
                ids = ids.Where(i => i.Contains(needle, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            int limit = 10;
            if (opts.TryGetValue("--limit", out var limitText) && !string.IsNullOrWhiteSpace(limitText)
                && (!int.TryParse(limitText, out limit) || limit < 1))
            {
                _ui.Show("--limit must be a positive integer.");
                return;
            }

            int downloaded = 0;
            int cached = 0;
            var failed = new List<string>();

            foreach (string id in ids.Take(limit))
            {
                try
                {
                    var image = DownloadPortrait(Wait(_api.GetCharacterAsync(id)));
                    if (image.FromCache) cached++; else downloaded++;
                }
                catch (Exception ex) when (ex is RemoteApiException || ex is InvalidDataException || ex is InvalidOperationException)
                {
                    failed.Add(id);
                }
            }

            _ui.Show($"Portraits: {downloaded} downloaded, {cached} taken from the cache, {failed.Count} failed (folder: {_images!.Folder}).");
            if (failed.Count > 0)
            {
                _ui.Show("  Failed: " + string.Join(", ", failed));
            }
        }

        // remote cache [clear]
        private void HandleCache(string[] rest)
        {
            if (!RequireImages())
            {
                return;
            }

            if (rest.Length > 0 && rest[0].Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                _ui.Show($"Cache cleared: {_images!.Clear()} file(s) removed.");
                return;
            }

            var files = _images!.List();
            _ui.Show($"=== PICTURE CACHE ({files.Count} file(s), {_images.TotalBytes} bytes) ===");
            _ui.Show($"  Folder: {_images.Folder}");
            foreach (var file in files)
            {
                _ui.Show($"  {file.Key} ({file.Size} bytes, {file.SavedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm})");
            }
        }

        private CachedImage DownloadPortrait(RemoteCharacter remote)
        {
            if (string.IsNullOrWhiteSpace(remote.ImageUrl))
            {
                throw new InvalidOperationException($"{remote.Name} has no link to a picture.");
            }
            return Wait(_images!.GetAsync($"{remote.Id}-portrait", remote.ImageUrl));
        }

        private bool RequireImages()
        {
            if (_images != null)
            {
                return true;
            }
            _ui.Show("The picture cache is not configured.");
            return false;
        }
    }
}