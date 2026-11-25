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
            int health = Roll(HealthRange(rarity));
            int armor = Roll(ArmorRange(rarity));
            int attack = Roll(AttackRange(rarity));
            double resist = Roll(ResistPercentRange(rarity)) / 100.0;
            int speed = Roll(SpeedRange(rarity));

            return new CharacterStats(health, armor, attack, resist, speed);
        }

        public static (int Min, int Max) HealthRange(int rarity) =>
            rarity >= 5 ? (110, 140) : rarity == 4 ? (85, 115) : (60, 90);

        public static (int Min, int Max) ArmorRange(int rarity) =>
            rarity >= 5 ? (6, 10) : rarity == 4 ? (4, 8) : (2, 5);

        public static (int Min, int Max) AttackRange(int rarity) =>
            rarity >= 5 ? (18, 25) : rarity == 4 ? (14, 20) : (10, 14);

        /// <summary>Resist in percent.</summary>
        public static (int Min, int Max) ResistPercentRange(int rarity) =>
            rarity >= 5 ? (5, 20) : rarity == 4 ? (0, 12) : (0, 5);

        public static (int Min, int Max) SpeedRange(int rarity) =>
            rarity >= 5 ? (95, 130) : rarity == 4 ? (90, 120) : (85, 105);

        private int Roll((int Min, int Max) range) => _random.Next(range.Min, range.Max + 1);
    }
}