namespace GameSystem.Core.Models
{
    /// <summary>Represents an equippable item with immutable stat bonuses.</summary>
    public class Item
    {
        public string Name { get; }
        public int HealthBonus { get; }
        public int ArmorBonus { get; }
        public int AttackBonus { get; }
        public string Description { get; }

        public Item(string name, int healthBonus = 0, int armorBonus = 0, int attackBonus = 0, string description = "")
        {
            Name = name;
            HealthBonus = healthBonus;
            ArmorBonus = armorBonus;
            AttackBonus = attackBonus;
            Description = description ?? string.Empty;
        }
    }
}