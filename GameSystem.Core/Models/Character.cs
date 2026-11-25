using System;
using System.Collections.Generic;

using GameSystem.Core.Abilities;
using GameSystem.Core.Interfaces;

namespace GameSystem.Core.Models
{
    /// <summary>
    /// Represents a base character entity with strict encapsulation.
    /// </summary>
    public abstract class Character
    {
        private int _health;
        private int _maxHealth;
        private int _armor;
        private int _attackPower;
        private bool _isDefending;
        private readonly List<IAbility> _learnedAbilities = new List<IAbility>();

        public string Name { get; }
        public int Health => _health;
        public int MaxHealth => _maxHealth;
        public int Armor => _armor;
        public int AttackPower => _attackPower;
        public bool IsAlive => _health > 0;
        public bool IsDefending => _isDefending;
        /// <summary>The fraction (0..1) of the damage that the character ignores.</summary>
        public double Resist { get; private set; }

        /// <summary>Initiative: 100 is one action per round; more means acting earlier and sometimes more than once.</summary>
        public int Speed { get; private set; } = 100;
        public Item? EquippedItem { get; private set; }
        public IAbility UniqueAbility { get; }

        /// <summary>Abilities learned in addition to the unique one.</summary>
        public IReadOnlyList<IAbility> LearnedAbilities => _learnedAbilities;

        protected Character(string name, int maxHealth, int armor, int attackPower, IAbility uniqueAbility)
        {
            Name = name;
            _maxHealth = maxHealth;
            _health = maxHealth;
            _armor = armor;
            _attackPower = attackPower;
            UniqueAbility = uniqueAbility;
        }

        public void Equip(Item item, IGameLogger logger)
        {
            if (ReferenceEquals(EquippedItem, item))
            {
                logger.Log($"{Name} already has {item.Name} equipped.");
                return;
            }

            if (EquippedItem != null)
            {
                Unequip(EquippedItem, logger);
            }

            EquippedItem = item;
            _maxHealth += item.HealthBonus;
            _health += item.HealthBonus;
            _armor += item.ArmorBonus;
            _attackPower += item.AttackBonus;
            logger.Log($"{Name} equipped {item.Name} (+{item.AttackBonus} ATK, +{item.ArmorBonus} ARM, +{item.HealthBonus} HP).");
        }

        private void Unequip(Item old, IGameLogger logger)
        {
            bool wasAlive = IsAlive;
            _maxHealth -= old.HealthBonus;
            _health = Math.Min(_health, _maxHealth);
            if (wasAlive && _health < 1)
            {
                _health = 1;
            }
            _armor = Math.Max(0, _armor - old.ArmorBonus);
            _attackPower = Math.Max(0, _attackPower - old.AttackBonus);
            EquippedItem = null;
            logger.Log($"{Name} unequipped {old.Name}.");
        }

        public bool HasAbility(IAbility ability)
        {
            return ReferenceEquals(UniqueAbility, ability) || _learnedAbilities.Contains(ability);
        }

        public bool LearnAbility(IAbility ability, IGameLogger logger)
        {
            if (HasAbility(ability))
            {
                logger.Log($"{Name} already knows {ability.Name}.");
                return false;
            }

            _learnedAbilities.Add(ability);
            logger.Log($"{Name} learned {ability.Name}.");
            return true;
        }

        /// <summary>A plain physical attack.</summary>
        public virtual void Attack(Character target, IGameLogger logger) =>
            Attack(target, logger, AttackProfile.Physical);

        /// <summary>An attack with the given profile (physical or magic, with or without resist penetration).</summary>
        public virtual void Attack(Character target, IGameLogger logger, AttackProfile profile)
        {
            if (!IsAlive) return;

            if (!target.IsAlive)
            {
                logger.Log($"{target.Name} is already defeated.");
                return;
            }

            int damage = DamageCalculator.Calculate(
                _attackPower, target.Armor, target.Resist, target.IsDefending, profile, GameRules.Default);

            logger.Log(profile.Kind == DamageKind.Magic
                ? $"{Name} hits {target.Name} with magic for {damage} damage."
                : $"{Name} attacks {target.Name} for {damage} damage.");
            target.TakeDamage(damage, logger);
        }

        public void UseSpecialAbility(Character target, IGameLogger logger)
        {
            if (!IsAlive) return;

            if (UniqueAbility != null)
            {
                UniqueAbility.Execute(this, target, logger);
            }
            else
            {
                logger.Log($"{Name} has no special ability to use!");
            }
        }

        /// <summary>Uses any ability this character knows (unique or learned).</summary>
        public void UseAbility(IAbility ability, Character target, IGameLogger logger)
        {
            if (!IsAlive) return;

            if (!HasAbility(ability))
            {
                logger.Log($"{Name} does not know {ability.Name}.");
                return;
            }

            ability.Execute(this, target, logger);
        }
        /// <summary>Restores a previously saved state (used by persistence). No logging, no bonus recalculation.</summary>
        public void RestoreState(int health, int maxHealth, int armor, int attackPower,
                                 Item? equippedItem, IEnumerable<IAbility> learnedAbilities)
        {
            _maxHealth = Math.Max(1, maxHealth);
            _health = Math.Clamp(health, 0, _maxHealth);
            _armor = Math.Max(0, armor);
            _attackPower = Math.Max(0, attackPower);
            _isDefending = false;
            EquippedItem = equippedItem;

            _learnedAbilities.Clear();
            foreach (var ability in learnedAbilities)
            {
                if (!HasAbility(ability))
                {
                    _learnedAbilities.Add(ability);
                }
            }
        }
        /// <summary>Sets the combat stats that are not part of the constructor.</summary>
        public void SetCombatStats(double resist, int speed)
        {
            if (resist < 0.0 || resist > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(resist), "Resist must be between 0 and 1.");
            }
            if (speed < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(speed), "Speed must be at least 1.");
            }

            Resist = resist;
            Speed = speed;
        }
        public void Heal(int amount, IGameLogger logger)
        {
            if (!IsAlive) return;

            _health = Math.Min(_maxHealth, _health + amount);
            logger.Log($"{Name} healed for {amount} HP. Current Health: {_health}/{_maxHealth}.");
        }

        public void PrepareDefense(IGameLogger logger)
        {
            _isDefending = true;
            logger.Log($"{Name} assumes a defensive stance!");
        }

        public void ResetDefense()
        {
            _isDefending = false;
        }

        public void ModifyAttackPower(int amount) => _attackPower = Math.Max(0, _attackPower + amount);
        public void ModifyArmor(int amount) => _armor = Math.Max(0, _armor + amount);

        private void TakeDamage(int damage, IGameLogger logger)
        {
            _health = Math.Max(0, _health - damage);
            if (!IsAlive)
            {
                logger.Log($"{Name} has been defeated!");
            }
        }
    }
}