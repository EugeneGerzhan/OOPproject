using System;
using System.IO;
using System.Linq;
using GameSystem.Core.Abilities;
using GameSystem.Core.Dto;
using GameSystem.Core.Interfaces;
using GameSystem.Core.Mapping;
using GameSystem.Core.Models;
using Infra.Data;
using Moq;
using ProjectTests.Mocks;
using Xunit;

namespace ProjectTests.CharacterTests
{
    /// <summary>State with Growler (id 1) and Shi Fuu (id 2) and their unique abilities (ids 1 and 2).</summary>
    public static class TestWorld
    {
        public static (GameState state, Character barbarian, Character monk) Build()
        {
            var state = new GameState();
            var barbarian = new HalfOrcBarbarian("Growler");
            var monk = new Monk("Shi Fuu");
            state.Characters.Add(barbarian);
            state.Characters.Add(monk);
            state.Abilities.Add(barbarian.UniqueAbility);
            state.Abilities.Add(monk.UniqueAbility);
            return (state, barbarian, monk);
        }
    }

    public class DtoTests
    {
        private readonly IGameLogger _logger = Mock.Of<IGameLogger>();

        private static string[] SplitLines(string text) =>
            text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

        [Fact]
        public void CharacterDto_Short_ShowsIdNameStatsAndStatus()
        {
            // Arrange
            var (state, barbarian, _) = TestWorld.Build();

            // Act
            string text = new CharacterDto(state, 1, barbarian, false).Render();

            // Assert
            Assert.Equal("  [1] Growler (HalfOrcBarbarian) HP 120/120, ARM 5, ATK 20, alive", text);
        }

        [Fact]
        public void CharacterDto_Short_ShowsDefeated()
        {
            // Arrange
            var (state, _, monk) = TestWorld.Build();
            monk.RestoreState(0, 90, 8, 15, null, Array.Empty<IAbility>());

            // Act
            string text = new CharacterDto(state, 2, monk, false).Render();

            // Assert
            Assert.Equal("  [2] Shi Fuu (Monk) HP 0/90, ARM 8, ATK 15, defeated", text);
        }

        [Fact]
        public void CharacterDto_Short_ShowsDefendingStance()
        {
            // Arrange
            var (state, _, monk) = TestWorld.Build();
            monk.PrepareDefense(_logger);

            // Act
            string text = new CharacterDto(state, 2, monk, false).Render();

            // Assert
            Assert.EndsWith("alive, defending", text);
        }

        [Fact]
        public void CharacterDto_Detailed_ListsItemAndAbilities()
        {
            // Arrange
            var (state, barbarian, _) = TestWorld.Build();
            var axe = new Item("Axe", attackBonus: 5);
            state.Items.Add(axe);
            barbarian.Equip(axe, _logger);

            // Act
            var lines = SplitLines(new CharacterDto(state, 1, barbarian, true).Render());

            // Assert
            Assert.Equal(9, lines.Length);
            Assert.Equal("=== CHARACTER [1] Growler ===", lines[0]);
            Assert.Contains("[1] Axe (+5 ATK, +0 ARM, +0 HP)", lines[6]);
            Assert.Contains("[1] Rage", lines[7]);
            Assert.Contains("none", lines[8]);
        }

        [Fact]
        public void ItemDto_Short_ShowsWhoUsesIt()
        {
            // Arrange
            var (state, barbarian, _) = TestWorld.Build();
            var axe = new Item("Axe", attackBonus: 5);
            state.Items.Add(axe);

            // Act
            string unused = new ItemDto(state, 1, axe, false).Render();
            barbarian.Equip(axe, _logger);
            string used = new ItemDto(state, 1, axe, false).Render();

            // Assert
            Assert.Equal("  [1] Axe (+5 ATK, +0 ARM, +0 HP) | used by: nobody", unused);
            Assert.Equal("  [1] Axe (+5 ATK, +0 ARM, +0 HP) | used by: Growler", used);
        }

        [Fact]
        public void ItemDto_Detailed_HasHeaderAndFourDetailLines()
        {
            // Arrange
            var (state, _, _) = TestWorld.Build();
            var axe = new Item("Axe", attackBonus: 5);
            state.Items.Add(axe);

            // Act
            var lines = SplitLines(new ItemDto(state, 1, axe, true).Render());

            // Assert
            Assert.Equal(5, lines.Length);
            Assert.Equal("=== ITEM [1] Axe ===", lines[0]);
        }

        [Fact]
        public void AbilityDto_Short_ShowsUsers()
        {
            // Arrange
            var (state, _, _) = TestWorld.Build();
            var rage = state.Abilities.TryGet(1, out var found) ? found : null;

            // Act
            string text = new AbilityDto(state, 1, rage!, false).Render();

            // Assert
            Assert.Equal("  [1] Rage | can be used by: Growler", text);
        }

        [Fact]
        public void AbilityDto_Detailed_ShowsTypeAndEffect()
        {
            // Arrange
            var (state, _, _) = TestWorld.Build();
            var whirlwind = new CustomAbility("Whirlwind", 3, 2);
            state.Abilities.Add(whirlwind);

            // Act
            string text = new AbilityDto(state, 3, whirlwind, true).Render();

            // Assert
            Assert.Contains("=== ABILITY [3] Whirlwind ===", text);
            Assert.Contains("CustomAbility", text);
            Assert.Contains("3 attack(s) with +2 ATK while used", text);
            Assert.Contains("nobody", text);
        }
    }

    public class CharacterSaveMapperTests
    {
        private readonly IGameLogger _logger = Mock.Of<IGameLogger>();

        /// <summary>Items: Axe(1), Shield(2). Abilities: Rage(1), Flurry(2), Whirlwind(3).
        /// Growler wears Axe and knows Whirlwind, Shi Fuu wears Shield and was attacked.</summary>
        private (GameState state, Character barbarian, Character monk) BuildFullWorld()
        {
            var (state, barbarian, monk) = TestWorld.Build();
            var axe = new Item("Axe", attackBonus: 5);
            var shield = new Item("Shield", armorBonus: 3);
            var whirlwind = new CustomAbility("Whirlwind", 3, 2);
            state.Items.Add(axe);
            state.Items.Add(shield);
            state.Abilities.Add(whirlwind);

            barbarian.Equip(axe, _logger);
            monk.Equip(shield, _logger);
            barbarian.LearnAbility(whirlwind, _logger);
            barbarian.Attack(monk, _logger);
            return (state, barbarian, monk);
        }

        [Fact]
        public void ToSave_WritesAllEntitiesAndLinks()
        {
            // Arrange
            var (state, _, _) = BuildFullWorld();

            // Act
            var save = CharacterSaveMapper.ToSave("arena", state);

            // Assert
            Assert.Equal("arena", save.SaveName);
            Assert.Equal(2, save.Characters.Count);
            Assert.Equal(2, save.Items.Count);
            Assert.Equal(3, save.Abilities.Count);
            Assert.Equal(2, save.ItemLinks.Count);
            Assert.Contains(save.ItemLinks, l => l.CharacterId == 1 && l.ItemId == 1);
            Assert.Contains(save.ItemLinks, l => l.CharacterId == 2 && l.ItemId == 2);
            var abilityLink = Assert.Single(save.AbilityLinks);
            Assert.Equal(1, abilityLink.CharacterId);
            Assert.Equal(3, abilityLink.AbilityId);
        }

        [Fact]
        public void ToSave_DescribesCharactersAndAbilities()
        {
            // Arrange
            var (state, _, _) = BuildFullWorld();

            // Act
            var save = CharacterSaveMapper.ToSave("arena", state);

            // Assert
            var growler = save.Characters.Single(c => c.Id == 1);
            Assert.Equal("Growler", growler.Name);
            Assert.Equal("barbarian", growler.Class);
            Assert.Equal(1, growler.UniqueAbilityId);

            Assert.Equal("rage", save.Abilities.Single(a => a.Id == 1).Type);
            Assert.Equal("flurry", save.Abilities.Single(a => a.Id == 2).Type);
            var custom = save.Abilities.Single(a => a.Id == 3);
            Assert.Equal("custom", custom.Type);
            Assert.Equal("Whirlwind", custom.Name);
            Assert.Equal(3, custom.Hits);
            Assert.Equal(2, custom.AttackBonus);
        }

        [Fact]
        public void RoundTrip_RestoresStatsItemsAndAbilities()
        {
            // Arrange
            var (state, _, _) = BuildFullWorld();
            var save = CharacterSaveMapper.ToSave("arena", state);

            // Act
            var restored = CharacterSaveMapper.ToGameState(save);

            // Assert
            Assert.Equal(state.Characters.Count, restored.Characters.Count);
            Assert.Equal(state.Items.Count, restored.Items.Count);
            Assert.Equal(state.Abilities.Count, restored.Abilities.Count);

            foreach (var entry in state.Characters.Entries)
            {
                Assert.True(restored.Characters.TryGet(entry.Key, out var copy));
                var original = entry.Value;
                Assert.Equal(original.Name, copy.Name);
                Assert.Equal(original.GetType(), copy.GetType());
                Assert.Equal(original.Health, copy.Health);
                Assert.Equal(original.MaxHealth, copy.MaxHealth);
                Assert.Equal(original.Armor, copy.Armor);
                Assert.Equal(original.AttackPower, copy.AttackPower);
                Assert.Equal(original.EquippedItem?.Name, copy.EquippedItem?.Name);
                Assert.Equal(
                    original.LearnedAbilities.Select(a => a.Name),
                    copy.LearnedAbilities.Select(a => a.Name));
            }
        }

        [Fact]
        public void RoundTrip_KeepsIdentityBetweenRegistriesAndCharacters()
        {
            // Arrange
            var (state, _, _) = BuildFullWorld();
            var save = CharacterSaveMapper.ToSave("arena", state);

            // Act
            var restored = CharacterSaveMapper.ToGameState(save);

            // Assert
            restored.Characters.TryGet(1, out var growler);
            Assert.Equal(1, restored.Abilities.IdOf(growler!.UniqueAbility));
            Assert.Equal(1, restored.Items.IdOf(growler.EquippedItem!));
            var learned = Assert.Single(growler.LearnedAbilities);
            Assert.Equal(3, restored.Abilities.IdOf(learned));

            var custom = Assert.IsType<CustomAbility>(learned);
            Assert.Equal(3, custom.Hits);
            Assert.Equal(2, custom.AttackBonus);
        }

        [Fact]
        public void ToGameState_EmptySave_GivesEmptyState()
        {
            // Act
            var state = CharacterSaveMapper.ToGameState(new CharacterSave());

            // Assert
            Assert.Equal(0, state.Characters.Count);
            Assert.Equal(0, state.Items.Count);
            Assert.Equal(0, state.Abilities.Count);
        }

        [Fact]
        public void ToGameState_UnknownAbilityType_Throws()
        {
            // Arrange
            var save = new CharacterSave();
            save.Abilities.Add(new AbilityData(1, "laser", "Laser", 0, 0));

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => CharacterSaveMapper.ToGameState(save));
        }

        [Fact]
        public void ToGameState_UnknownCharacterClass_Throws()
        {
            // Arrange
            var save = new CharacterSave();
            save.Characters.Add(new CharacterData(1, "Merlin", "wizard", 10, 10, 1, 1, 0));

            // Act & Assert
            Assert.Throws<ArgumentException>(() => CharacterSaveMapper.ToGameState(save));
        }

        [Fact]
        public void ToGameState_LinkToMissingItem_Throws()
        {
            // Arrange
            var save = new CharacterSave();
            save.Characters.Add(new CharacterData(1, "Growler", "barbarian", 120, 120, 5, 20, 0));
            save.ItemLinks.Add(new CharacterItems(1, 1, 99));

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => CharacterSaveMapper.ToGameState(save));
        }

        [Fact]
        public void ToGameState_LinkToMissingAbility_Throws()
        {
            // Arrange
            var save = new CharacterSave();
            save.Characters.Add(new CharacterData(1, "Growler", "barbarian", 120, 120, 5, 20, 0));
            save.AbilityLinks.Add(new CharacterAbilities(1, 1, 99));

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => CharacterSaveMapper.ToGameState(save));
        }

        [Fact]
        public void ToGameState_DuplicateItemIds_Throws()
        {
            // Arrange
            var save = new CharacterSave();
            save.Items.Add(new ItemData(1, "A", 0, 0, 0));
            save.Items.Add(new ItemData(1, "B", 0, 0, 0));

            // Act & Assert
            Assert.Throws<ArgumentException>(() => CharacterSaveMapper.ToGameState(save));
        }

        [Fact]
        public void ToSave_UnsupportedAbilityType_Throws()
        {
            // Arrange
            var state = new GameState();
            state.Abilities.Add(new AbilityMock());

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => CharacterSaveMapper.ToSave("x", state));
        }
    }
}