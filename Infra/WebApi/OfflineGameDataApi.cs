using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infra.WebApi
{
    /// <summary>Offline replacement of the remote service with a few embedded samples (for demos without internet).</summary>
    public class OfflineGameDataApi : IGameDataApi
    {
        private static string Q(string text) => text.Replace('\'', '"');

        private static readonly Dictionary<string, string> CharacterJson =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["albedo"] = Q("{ 'name': 'Albedo', 'title': 'Kreideprinz', 'vision': 'Geo', 'weapon': 'Sword', " +
                    "'nation': 'Mondstadt', 'affiliation': 'Knights of Favonius', 'rarity': 5, " +
                    "'description': 'Chief Alchemist of the Knights of Favonius and captain of its Investigation Team.', " +
                    "'skillTalents': [ " +
                    "{ 'name': 'Favonius Bladework - Weiss', 'unlock': 'Normal Attack', 'description': 'Performs up to five rapid sword strikes.' }, " +
                    "{ 'name': 'Abiogenesis: Solar Isotoma', 'unlock': 'Elemental Skill', 'description': 'Creates a Solar Isotoma that deals Geo damage to nearby enemies.' }, " +
                    "{ 'name': 'Rite of Progeniture: Tectonic Tide', 'unlock': 'Elemental Burst', 'description': 'Unleashes Geo-infused blades at enemies in front of him.' } ], " +
                    "'passiveTalents': [ { 'name': 'Calcite Might', 'unlock': 'Unlocked at Ascension 1', 'description': 'Strengthens the Solar Isotoma.' } ] }"),

                ["amber"] = Q("{ 'name': 'Amber', 'title': 'Gliding Champion', 'vision': 'Pyro', 'weapon': 'Bow', " +
                    "'nation': 'Mondstadt', 'affiliation': 'Knights of Favonius', 'rarity': 4, " +
                    "'description': 'Outrider of the Knights of Favonius, a scout and a skilled glider.', " +
                    "'skillTalents': [ " +
                    "{ 'name': 'Sharpshooter', 'unlock': 'Normal Attack', 'description': 'Fires arrows in quick succession.' }, " +
                    "{ 'name': 'Explosive Puppet', 'unlock': 'Elemental Skill', 'description': 'Throws a decoy that explodes with Pyro damage.' }, " +
                    "{ 'name': 'Fiery Rain', 'unlock': 'Elemental Burst', 'description': 'Fires a barrage of flaming arrows.' } ], " +
                    "'passiveTalents': [] }")
            };

        private static readonly Dictionary<string, string> WeaponJson =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["skyward-blade"] = Q("{ 'name': 'Skyward Blade', 'type': 'Sword', 'rarity': 5, 'baseAttack': 46, " +
                    "'subStat': 'Energy Recharge', 'passiveName': 'Sky-Piercing Fang', " +
                    "'passiveDesc': 'Raises CRIT Rate and empowers attacks after an Elemental Burst.', 'location': 'Gacha' }"),

                ["favonius-sword"] = Q("{ 'name': 'Favonius Sword', 'type': 'Sword', 'rarity': 4, 'baseAttack': 41, " +
                    "'subStat': 'Energy Recharge', 'passiveName': 'Windfall', " +
                    "'passiveDesc': 'Critical hits may generate Elemental Particles.', 'location': 'Gacha' }")
            };

        public Task<IReadOnlyList<string>> GetCharacterIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(CharacterJson.Keys.ToList());

        public Task<IReadOnlyList<string>> GetWeaponIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(WeaponJson.Keys.ToList());

        public Task<RemoteCharacter> GetCharacterAsync(string id, CancellationToken cancellationToken = default)
        {
            string key = GenshinApiClient.NormalizeId(id);
            if (!CharacterJson.TryGetValue(key, out var json))
            {
                throw new RemoteApiException($"Character '{id.Trim()}' was not found in the offline sample data.");
            }
            var character = GenshinJsonParser.ParseCharacter(key, json);
            return Task.FromResult(character with { ImageUrl = $"https://genshin.jmp.blue/characters/{key}/card" });
        }

        public Task<RemoteWeapon> GetWeaponAsync(string id, CancellationToken cancellationToken = default)
        {
            string key = GenshinApiClient.NormalizeId(id);
            if (!WeaponJson.TryGetValue(key, out var json))
            {
                throw new RemoteApiException($"Weapon '{id.Trim()}' was not found in the offline sample data.");
            }
            return Task.FromResult(GenshinJsonParser.ParseWeapon(key, json));
        }
    }
}