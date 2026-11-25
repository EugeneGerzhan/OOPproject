using System;
using GameSystem.Core.Interfaces;
using GameSystem.Core.Models;

namespace GameSystem.Core.Abilities
{
    /// <summary>
    /// Configurable ability: N consecutive attacks with a temporary attack bonus, physical or magic.
    /// Created from the CLI dialog or translated from a talent of the remote database.
    /// </summary>
    public class CustomAbility : IAbility
    {
        public string Name { get; }
        public int Hits { get; }
        public int AttackBonus { get; }

        /// <summary>Flavour text (for imported talents); does not affect the mechanics.</summary>
        public string Description { get; }

        public DamageKind Kind { get; }

        /// <summary>The fraction (0..1) of the target's resist that the hits ignore.</summary>
        public double ResistPenetration { get; }

        public CustomAbility(string name, int hits, int attackBonus, string description = "",
                             DamageKind kind = DamageKind.Physical, double resistPenetration = 0.0)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Ability name cannot be empty.");
            }
            if (hits < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(hits), "Hits must be at least 1.");
            }
            if (attackBonus < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attackBonus), "Attack bonus cannot be negative.");
            }
            if (resistPenetration < 0.0 || resistPenetration > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(resistPenetration), "Resist penetration must be between 0 and 1.");
            }

            Name = name.Trim();
            Hits = hits;
            AttackBonus = attackBonus;
            Description = description ?? string.Empty;
            Kind = kind;
            ResistPenetration = resistPenetration;
        }

        public void Execute(Character source, Character target, IGameLogger logger)
        {
            logger.Log($"{source.Name} uses {Name}!");

            var profile = new AttackProfile(Kind, ResistPenetration);
            source.ModifyAttackPower(AttackBonus);
            for (int i = 0; i < Hits; i++)
            {
                source.Attack(target, logger, profile);
            }
            source.ModifyAttackPower(-AttackBonus);
        }

        public string Describe()
        {
            string kind = Kind == DamageKind.Magic ? "magic " : string.Empty;
            string penetration = ResistPenetration > 0.0
                ? $", ignores {Math.Round(ResistPenetration * 100)}% of resist"
                : string.Empty;
            return $"{Hits} {kind}attack(s) with +{AttackBonus} ATK while used{penetration}";
        }
    }
}