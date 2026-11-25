using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GameSystem.Core.Abilities;
using GameSystem.Core.Interfaces;
using GameSystem.Core.Models;
using Infra.WebApi;

namespace GameSystem.Core.Remote
{
    public record ImportedCharacter(int CharacterId, ApiCharacter Character, int UniqueAbilityId, IReadOnlyList<int> LearnedAbilityIds);

    public record ImportedItem(int ItemId, Item Item);

    /// <summary>Translates remote DTOs into domain objects and registers them in a GameState.</summary>
    public class RemoteImporter
    {
        private sealed class SilentLogger : IGameLogger
        {
            public void Log(string message) { }
        }

        private readonly StatGenerator _stats;

        public RemoteImporter(StatGenerator stats)
        {
            _stats = stats;
        }

        // ---------- pure translation ----------

        public ApiCharacter ToCharacter(RemoteCharacter remote)
        {
            var talents = remote.SkillTalents.Where(t => !string.IsNullOrWhiteSpace(t.Name)).ToList();
            var abilities = talents.Select(ToAbility).ToList();

            int uniqueIndex = PickUniqueIndex(talents);
            IAbility unique = abilities.Count > 0
                ? abilities[uniqueIndex]
                : new CustomAbility("Basic Strike", 1, 0, "A plain attack.");

            var profile = new CharacterProfile(
                remote.Id, remote.Title, remote.Vision, remote.WeaponType,
                remote.Nation, remote.Affiliation, Clean(remote.Description), remote.Rarity);

            var character = new ApiCharacter(remote.Name.Trim(), profile, _stats.Generate(remote.Rarity), unique);

            var logger = new SilentLogger();
            for (int i = 0; i < abilities.Count; i++)
            {
                if (i != uniqueIndex)
                {
                    character.LearnAbility(abilities[i], logger);
                }
            }

            return character;
        }

        public Item ToItem(RemoteWeapon remote)
        {
            int rarity = Math.Max(0, remote.Rarity);
            int attack = Math.Max(1, (int)Math.Round(remote.BaseAttack / 5.0, MidpointRounding.AwayFromZero));
            int health = rarity * 2;
            int armor = rarity / 2;

            return new Item(remote.Name.Trim(), health, armor, attack, BuildWeaponDescription(remote));
        }

        // ---------- registration ----------

        public ImportedCharacter ImportCharacter(RemoteCharacter remote, GameState state)
        {
            string name = remote.Name.Trim();
            if (state.Characters.NameExists(name))
            {
                throw new InvalidOperationException($"Character '{name}' is already in the game.");
            }

            var character = ToCharacter(remote);
            int characterId = state.Characters.Add(character);
            int uniqueId = state.Abilities.Add(character.UniqueAbility);
            var learnedIds = character.LearnedAbilities.Select(a => state.Abilities.Add(a)).ToList();

            return new ImportedCharacter(characterId, character, uniqueId, learnedIds);
        }

        public ImportedItem ImportWeapon(RemoteWeapon remote, GameState state)
        {
            string name = remote.Name.Trim();
            if (state.Items.NameExists(name))
            {
                throw new InvalidOperationException($"Item '{name}' is already in the game.");
            }

            var item = ToItem(remote);
            int id = state.Items.Add(item);
            return new ImportedItem(id, item);
        }

        // ---------- helpers ----------

        private static CustomAbility ToAbility(RemoteTalent talent)
        {
            var (hits, bonus, kind, penetration) = Mechanics(talent.Unlock);
            return new CustomAbility(talent.Name, hits, bonus, Clean(talent.Description), kind, penetration);
        }

        private static (int Hits, int Bonus, DamageKind Kind, double Penetration) Mechanics(string? unlock)
        {
            string name = (unlock ?? string.Empty).ToLowerInvariant();
            if (name.Contains("burst")) return (3, 6, DamageKind.Magic, 0.10);
            if (name.Contains("skill")) return (2, 3, DamageKind.Magic, 0.0);
            return (1, 0, DamageKind.Physical, 0.0);
        }

        private static int Rank(string? unlock)
        {
            string kind = (unlock ?? string.Empty).ToLowerInvariant();
            if (kind.Contains("burst")) return 2;
            if (kind.Contains("skill")) return 1;
            return 0;
        }

        private static int PickUniqueIndex(IReadOnlyList<RemoteTalent> talents)
        {
            int best = 0;
            int bestRank = -1;
            for (int i = 0; i < talents.Count; i++)
            {
                int rank = Rank(talents[i].Unlock);
                if (rank > bestRank)
                {
                    best = i;
                    bestRank = rank;
                }
            }
            return best;
        }

        private static string BuildWeaponDescription(RemoteWeapon weapon)
        {
            var header = new List<string>();
            if (!string.IsNullOrWhiteSpace(weapon.Type)) header.Add(weapon.Type.Trim());
            header.Add($"rarity {weapon.Rarity}");
            if (!string.IsNullOrWhiteSpace(weapon.SubStat)) header.Add(weapon.SubStat.Trim());

            var parts = new List<string> { string.Join(", ", header) };

            string passive = Clean(weapon.PassiveName);
            string passiveText = Clean(weapon.PassiveDescription);
            if (passive.Length > 0 && passiveText.Length > 0) parts.Add($"{passive}: {passiveText}");
            else if (passive.Length > 0) parts.Add(passive);
            else if (passiveText.Length > 0) parts.Add(passiveText);

            return string.Join(". ", parts);
        }

        private static string Clean(string? text) =>
            string.IsNullOrWhiteSpace(text) ? string.Empty : Regex.Replace(text.Trim(), @"\s+", " ");
    }
}