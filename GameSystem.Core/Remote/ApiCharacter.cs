using GameSystem.Core.Abilities;
using GameSystem.Core.Models;

namespace GameSystem.Core.Remote
{
    /// <summary>Descriptive data of a character that came from the remote database.</summary>
    public record CharacterProfile(string RemoteId, string Title, string Vision, string WeaponType,
                                   string Nation, string Affiliation, string Description, int Rarity);

    public record CharacterStats(int Health, int Armor, int AttackPower);

    /// <summary>A character built from remote data: the game stats are generated, the rest comes from the profile.</summary>
    public class ApiCharacter : Character
    {
        public CharacterProfile Profile { get; }

        public ApiCharacter(string name, CharacterProfile profile, CharacterStats stats, IAbility uniqueAbility)
            : base(name, stats.Health, stats.Armor, stats.AttackPower, uniqueAbility)
        {
            Profile = profile;
        }
    }
}