using System.Collections.Generic;
using System.Linq;
using GameSystem.Core.Abilities;

namespace GameSystem.Core.Models
{
    /// <summary>The whole state of the characters system: everything that can be saved and loaded.</summary>
    public class GameState
    {
        public Registry<Character> Characters { get; } = new Registry<Character>(c => c.Name);
        public Registry<Item> Items { get; } = new Registry<Item>(i => i.Name);
        public Registry<IAbility> Abilities { get; } = new Registry<IAbility>(a => a.Name);

        public IEnumerable<Character> UsersOf(Item item) =>
            Characters.Entries.Select(e => e.Value).Where(c => ReferenceEquals(c.EquippedItem, item));

        public IEnumerable<Character> UsersOf(IAbility ability) =>
            Characters.Entries.Select(e => e.Value).Where(c => c.HasAbility(ability));
    }
}
