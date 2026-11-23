using System.Collections.Generic;
using System.Linq;
using GameSystem.Core.Models;

namespace GameSystem.Engine
{
    /// <summary>One character that can be chosen for a battle.</summary>
    public record RosterRow(int Id, string Name, string ClassName, int Health, int MaxHealth, bool IsAlive);

    /// <summary>The data of the "available characters" view.</summary>
    public record RosterView(IReadOnlyList<RosterRow> Rows)
    {
        public static RosterView From(GameState state)
        {
            var rows = state.Characters.Entries
                .Select(e => new RosterRow(
                    e.Key, e.Value.Name, e.Value.GetType().Name, e.Value.Health, e.Value.MaxHealth, e.Value.IsAlive))
                .ToList();
            return new RosterView(rows);
        }
    }
}