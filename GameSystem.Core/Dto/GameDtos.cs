using System;
using System.Collections.Generic;
using System.Linq;
using GameSystem.Core.Abilities;
using GameSystem.Core.Models;
using Infra.Display;

namespace GameSystem.Core.Dto
{
    internal static class DtoFormat
    {
        public static string ItemLabel(GameState state, Item item) =>
            $"[{state.Items.IdOf(item)}] {item.Name} (+{item.AttackBonus} ATK, +{item.ArmorBonus} ARM, +{item.HealthBonus} HP)";

        public static string AbilityLabel(GameState state, IAbility ability) =>
            $"[{state.Abilities.IdOf(ability)}] {ability.Name}";

        public static string JoinNames(IEnumerable<Character> characters)
        {
            var names = characters.Select(c => c.Name).ToList();
            return names.Count == 0 ? "nobody" : string.Join(", ", names);
        }
    }

    /// <summary>
    /// TEMPLATE METHOD pattern. The skeleton of <see cref="Render"/> is fixed here: a short one-line form
    /// or a detailed multi-line form. The subclasses only fill in the two steps, so all DTOs of the game
    /// behave the same way and the choice between the forms is written once.
    /// </summary>
    public abstract class EntityDto : IDisplayable
    {
        private readonly bool _detailed;

        protected EntityDto(bool detailed)
        {
            _detailed = detailed;
        }

        public string Render() => _detailed ? string.Join(Environment.NewLine, DetailLines()) : ShortLine();

        /// <summary>Step 1 of the template: the one-line form (used in lists).</summary>
        protected abstract string ShortLine();

        /// <summary>Step 2 of the template: the lines of the detailed form (used for one object).</summary>
        protected abstract IEnumerable<string> DetailLines();
    }

    public class CharacterDto : EntityDto
    {
        public int Id { get; }
        public string Name { get; }
        public string ClassName { get; }
        public int Health { get; }
        public int MaxHealth { get; }
        public int Armor { get; }
        public int AttackPower { get; }
        public string Status { get; }
        public string EquippedItem { get; }
        public string UniqueAbility { get; }
        public string LearnedAbilities { get; }

        public CharacterDto(GameState state, int id, Character character, bool detailed) : base(detailed)
        {
            Id = id;
            Name = character.Name;
            ClassName = character.GetType().Name;
            Health = character.Health;
            MaxHealth = character.MaxHealth;
            Armor = character.Armor;
            AttackPower = character.AttackPower;
            Status = (character.IsAlive ? "alive" : "defeated") + (character.IsDefending ? ", defending" : string.Empty);
            EquippedItem = character.EquippedItem != null ? DtoFormat.ItemLabel(state, character.EquippedItem) : "none";
            UniqueAbility = DtoFormat.AbilityLabel(state, character.UniqueAbility);
            LearnedAbilities = character.LearnedAbilities.Count == 0
                ? "none"
                : string.Join(", ", character.LearnedAbilities.Select(a => DtoFormat.AbilityLabel(state, a)));
        }

        protected override string ShortLine() =>
            $"  [{Id}] {Name} ({ClassName}) HP {Health}/{MaxHealth}, ARM {Armor}, ATK {AttackPower}, {Status}";

        protected override IEnumerable<string> DetailLines()
        {
            yield return $"=== CHARACTER [{Id}] {Name} ===";
            yield return $"  Class:             {ClassName}";
            yield return $"  Status:            {Status}";
            yield return $"  Health:            {Health}/{MaxHealth}";
            yield return $"  Armor:             {Armor}";
            yield return $"  Attack:            {AttackPower}";
            yield return $"  Item:              {EquippedItem}";
            yield return $"  Unique ability:    {UniqueAbility}";
            yield return $"  Learned abilities: {LearnedAbilities}";
        }
    }

    public class ItemDto : EntityDto
    {
        public int Id { get; }
        public string Name { get; }
        public int AttackBonus { get; }
        public int ArmorBonus { get; }
        public int HealthBonus { get; }
        public string UsedBy { get; }

        public ItemDto(GameState state, int id, Item item, bool detailed) : base(detailed)
        {
            Id = id;
            Name = item.Name;
            AttackBonus = item.AttackBonus;
            ArmorBonus = item.ArmorBonus;
            HealthBonus = item.HealthBonus;
            UsedBy = DtoFormat.JoinNames(state.UsersOf(item));
        }

        protected override string ShortLine() =>
            $"  [{Id}] {Name} (+{AttackBonus} ATK, +{ArmorBonus} ARM, +{HealthBonus} HP) | used by: {UsedBy}";

        protected override IEnumerable<string> DetailLines()
        {
            yield return $"=== ITEM [{Id}] {Name} ===";
            yield return $"  Attack bonus: +{AttackBonus}";
            yield return $"  Armor bonus:  +{ArmorBonus}";
            yield return $"  Health bonus: +{HealthBonus}";
            yield return $"  Used by:      {UsedBy}";
        }
    }

    public class AbilityDto : EntityDto
    {
        public int Id { get; }
        public string Name { get; }
        public string TypeName { get; }
        public string Effect { get; }
        public string UsedBy { get; }

        public AbilityDto(GameState state, int id, IAbility ability, bool detailed) : base(detailed)
        {
            Id = id;
            Name = ability.Name;
            TypeName = ability.GetType().Name;
            Effect = ability.Describe();
            UsedBy = DtoFormat.JoinNames(state.UsersOf(ability));
        }

        protected override string ShortLine() => $"  [{Id}] {Name} | can be used by: {UsedBy}";

        protected override IEnumerable<string> DetailLines()
        {
            yield return $"=== ABILITY [{Id}] {Name} ===";
            yield return $"  Type:             {TypeName}";
            yield return $"  Effect:           {Effect}";
            yield return $"  Can be used by:   {UsedBy}";
        }
    }
}