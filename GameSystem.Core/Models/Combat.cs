using System;

namespace GameSystem.Core.Models
{
    /// <summary>The kind of damage of an attack.</summary>
    public enum DamageKind
    {
        /// <summary>Reduced by armor and by resist.</summary>
        Physical,

        /// <summary>Ignores armor, reduced only by resist.</summary>
        Magic
    }

    /// <summary>
    /// The constants of the rules of the game. All the formulas read their numbers from here,
    /// so the balance can be changed in one place (for example <c>MinimumDamage = 0</c> lets armor block a hit completely).
    /// </summary>
    public sealed record GameRules
    {
        public static GameRules Default { get; } = new GameRules();

        /// <summary>The smallest damage of a hit that is not blocked (chip damage).</summary>
        public int MinimumDamage { get; init; } = 1;

        /// <summary>Defending multiplies the armor against physical attacks by this number.</summary>
        public double DefenseArmorMultiplier { get; init; } = 2.0;

        /// <summary>Defending adds this much resist against magic.</summary>
        public double DefenseResistBonus { get; init; } = 0.25;

        /// <summary>The largest resist that counts (the rest is ignored).</summary>
        public double MaxResist { get; init; } = 0.9;

        /// <summary>Magic does not feel armor, so its attack is scaled down by this number.</summary>
        public double MagicScale { get; init; } = 0.75;

        /// <summary>The speed that gives exactly one action per round.</summary>
        public int BaseSpeed { get; init; } = 100;
    }

    /// <summary>How an attack hurts: its kind and how much of the target's resist it ignores (0..1).</summary>
    public sealed record AttackProfile(DamageKind Kind = DamageKind.Physical, double ResistPenetration = 0.0)
    {
        public static AttackProfile Physical { get; } = new AttackProfile();
    }

    /// <summary>
    /// The damage formulas (see the design document):
    /// <code>
    /// physical: base = max(MinDamage, attack - armor x (2 if the target defends else 1))
    /// magic:    base = max(MinDamage, attack x MagicScale)
    /// resist_eff = clamp(resist - penetration [+ 0.25 if the target defends against magic], 0, MaxResist)
    /// damage = max(MinDamage, round(base x (1 - resist_eff)))
    /// </code>
    /// </summary>
    public static class DamageCalculator
    {
        public static int Calculate(int attack, int armor, double resist, bool targetDefending,
                                    AttackProfile profile, GameRules rules)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(rules);

            double effectiveResist = resist - profile.ResistPenetration;
            double baseDamage;

            if (profile.Kind == DamageKind.Magic)
            {
                baseDamage = Math.Max(rules.MinimumDamage, attack * rules.MagicScale);
                if (targetDefending)
                {
                    effectiveResist += rules.DefenseResistBonus;
                }
            }
            else
            {
                double effectiveArmor = armor * (targetDefending ? rules.DefenseArmorMultiplier : 1.0);
                baseDamage = Math.Max(rules.MinimumDamage, attack - effectiveArmor);
            }

            effectiveResist = Math.Clamp(effectiveResist, 0.0, rules.MaxResist);

            int damage = (int)Math.Round(baseDamage * (1.0 - effectiveResist), MidpointRounding.AwayFromZero);
            return Math.Max(rules.MinimumDamage, damage);
        }
    }
}