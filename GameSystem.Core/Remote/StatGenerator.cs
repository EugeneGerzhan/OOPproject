using System;

namespace GameSystem.Core.Remote
{
    /// <summary>The remote database has no stats, so they are generated from a range that depends on rarity.</summary>
    public class StatGenerator
    {
        private readonly Random _random;

        public StatGenerator(Random? random = null)
        {
            _random = random ?? new Random();
        }

        public CharacterStats Generate(int rarity)
        {
            return new CharacterStats(
                Roll(HealthRange(rarity)),
                Roll(ArmorRange(rarity)),
                Roll(AttackRange(rarity)));
        }

        public static (int Min, int Max) HealthRange(int rarity) =>
            rarity >= 5 ? (110, 140) : rarity == 4 ? (85, 115) : (60, 90);

        public static (int Min, int Max) ArmorRange(int rarity) =>
            rarity >= 5 ? (6, 10) : rarity == 4 ? (4, 8) : (2, 5);

        public static (int Min, int Max) AttackRange(int rarity) =>
            rarity >= 5 ? (18, 25) : rarity == 4 ? (14, 20) : (10, 14);

        private int Roll((int Min, int Max) range) => _random.Next(range.Min, range.Max + 1);
    }
}
