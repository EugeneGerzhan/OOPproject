using System;
using GameSystem.Core.Interfaces;
using GameSystem.Core.Models;

namespace GameSystem.Core.Abilities
{
    /// <summary>
    /// Configurable ability: N consecutive attacks with a temporary attack bonus.
    /// Created from the CLI dialog or translated from a talent of the remote database.
    /// </summary>
    public class CustomAbility : IAbility
    {
        public string Name { get; }
        public int Hits { get; }
        public int AttackBonus { get; }

        /// <summary>Flavour text (for imported talents); does not affect the mechanics.</summary>
        public string Description { get; }

        public CustomAbility(string name, int hits, int attackBonus, string description = "")
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

            Name = name.Trim();
            Hits = hits;
            AttackBonus = attackBonus;
            Description = description ?? string.Empty;
        }

        public void Execute(Character source, Character target, IGameLogger logger)
        {
            logger.Log($"{source.Name} uses {Name}!");
            source.ModifyAttackPower(AttackBonus);
            for (int i = 0; i < Hits; i++)
            {
                source.Attack(target, logger);
            }
            source.ModifyAttackPower(-AttackBonus);
        }

        public string Describe() => $"{Hits} attack(s) with +{AttackBonus} ATK while used";
    }
}