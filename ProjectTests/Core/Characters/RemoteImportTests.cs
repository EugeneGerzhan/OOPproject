using System;
using System.IO;
using System.Linq;
using GameSystem.Core.Abilities;
using GameSystem.Core.Interfaces;
using GameSystem.Core.Mapping;
using GameSystem.Core.Models;
using GameSystem.Core.Remote;
using Infra.Data;
using Infra.WebApi;
using Moq;
using ProjectTests.Mocks;
using Xunit;

namespace ProjectTests.RemoteTests
{
    public class StatGeneratorTests
    {
        [Theory]
        [InlineData(5, 110, 140, 6, 10, 18, 25)]
        [InlineData(4, 85, 115, 4, 8, 14, 20)]
        [InlineData(3, 60, 90, 2, 5, 10, 14)]
        [InlineData(0, 60, 90, 2, 5, 10, 14)]
        public void Generate_StaysInsideTheRangeOfRarity(
            int rarity, int hpMin, int hpMax, int armorMin, int armorMax, int attackMin, int attackMax)
        {
            // Arrange
            var generator = new StatGenerator(new Random(42));

            // Act & Assert
            for (int i = 0; i < 200; i++)
            {
                var stats = generator.Generate(rarity);
                Assert.InRange(stats.Health, hpMin, hpMax);
                Assert.InRange(stats.Armor, armorMin, armorMax);
                Assert.InRange(stats.AttackPower, attackMin, attackMax);
            }
        }

        [Fact]
        public void Generate_SameSeed_GivesSameStats()
        {
            // Arrange
            var first = new StatGenerator(new Random(7));
            var second = new StatGenerator(new Random(7));

            // Act & Assert
            Assert.Equal(first.Generate(5), second.Generate(5));
        }
    }

    public class RemoteImporterTests
    {
        private readonly IGameLogger _logger = Mock.Of<IGameLogger>();

        private static RemoteImporter NewImporter() => new RemoteImporter(new StatGenerator(new Random(1)));

        // ---------- character translation ----------

        [Fact]
        public void ToCharacter_CopiesProfile()
        {
            // Act
            var character = NewImporter().ToCharacter(RemoteSamples.Albedo());

            // Assert
            Assert.Equal("Albedo", character.Name);
            Assert.Equal("albedo", character.Profile.RemoteId);
            Assert.Equal("Kreideprinz", character.Profile.Title);
            Assert.Equal("Geo", character.Profile.Vision);
            Assert.Equal("Sword", character.Profile.WeaponType);
            Assert.Equal("Mondstadt", character.Profile.Nation);
            Assert.Equal(5, character.Profile.Rarity);
        }

        [Fact]
        public void ToCharacter_GeneratesStatsForItsRarity()
        {
            // Act
            var character = NewImporter().ToCharacter(RemoteSamples.Albedo());

            // Assert
            Assert.InRange(character.MaxHealth, 110, 140);
            Assert.Equal(character.MaxHealth, character.Health);
            Assert.InRange(character.Armor, 6, 10);
            Assert.InRange(character.AttackPower, 18, 25);
        }

        [Fact]
        public void ToCharacter_BurstBecomesUniqueAbility_OthersAreLearned()
        {
            // Act
            var character = NewImporter().ToCharacter(RemoteSamples.Albedo());

            // Assert
            Assert.Equal("Rite of Progeniture: Tectonic Tide", character.UniqueAbility.Name);
            Assert.Equal(
                new[] { "Favonius Bladework - Weiss", "Abiogenesis: Solar Isotoma" },
                character.LearnedAbilities.Select(a => a.Name));
        }

        [Fact]
        public void ToCharacter_TalentKindDecidesTheMechanics()
        {
            // Act
            var character = NewImporter().ToCharacter(RemoteSamples.Albedo());

            // Assert
            var burst = Assert.IsType<CustomAbility>(character.UniqueAbility);
            Assert.Equal(3, burst.Hits);
            Assert.Equal(6, burst.AttackBonus);

            var normal = Assert.IsType<CustomAbility>(character.LearnedAbilities[0]);
            Assert.Equal(1, normal.Hits);
            Assert.Equal(0, normal.AttackBonus);

            var skill = Assert.IsType<CustomAbility>(character.LearnedAbilities[1]);
            Assert.Equal(2, skill.Hits);
            Assert.Equal(3, skill.AttackBonus);
        }

        [Fact]
        public void ToCharacter_WithoutBurst_SkillBecomesUniqueAbility()
        {
            // Arrange
            var remote = RemoteSamples.Simple("x", "X") with
            {
                SkillTalents = new[]
                {
                    new RemoteTalent("Hit", "Normal Attack", ""),
                    new RemoteTalent("Zap", "Elemental Skill", "")
                }
            };

            // Act
            var character = NewImporter().ToCharacter(remote);

            // Assert
            Assert.Equal("Zap", character.UniqueAbility.Name);
            Assert.Equal("Hit", Assert.Single(character.LearnedAbilities).Name);
        }

        [Fact]
        public void ToCharacter_WithoutTalents_GetsBasicStrike()
        {
            // Arrange
            var remote = RemoteSamples.Simple("x", "X") with { SkillTalents = Array.Empty<RemoteTalent>() };

            // Act
            var character = NewImporter().ToCharacter(remote);

            // Assert
            Assert.Equal("Basic Strike", character.UniqueAbility.Name);
            Assert.Empty(character.LearnedAbilities);
        }

        [Fact]
        public void ToCharacter_IgnoresTalentsWithoutName()
        {
            // Arrange
            var remote = RemoteSamples.Simple("x", "X") with
            {
                SkillTalents = new[]
                {
                    new RemoteTalent("  ", "Elemental Burst", "ignored"),
                    new RemoteTalent("Strike", "Normal Attack", "")
                }
            };

            // Act
            var character = NewImporter().ToCharacter(remote);

            // Assert
            Assert.Equal("Strike", character.UniqueAbility.Name);
            Assert.Empty(character.LearnedAbilities);
        }

        [Fact]
        public void ToCharacter_CleansWhitespaceInDescriptions()
        {
            // Arrange
            var remote = RemoteSamples.Simple("x", "X") with
            {
                SkillTalents = new[] { new RemoteTalent("Strike", "Normal Attack", "Line one.\n  Line   two.") }
            };

            // Act
            var character = NewImporter().ToCharacter(remote);

            // Assert
            var ability = Assert.IsType<CustomAbility>(character.UniqueAbility);
            Assert.Equal("Line one. Line two.", ability.Description);
        }

        // ---------- weapon translation ----------

        [Fact]
        public void ToItem_MapsWeaponToBonuses()
        {
            // Act
            var item = NewImporter().ToItem(RemoteSamples.SkywardBlade());

            // Assert
            Assert.Equal("Skyward Blade", item.Name);
            Assert.Equal(9, item.AttackBonus);   // 46 / 5
            Assert.Equal(10, item.HealthBonus);  // 2 x rarity
            Assert.Equal(2, item.ArmorBonus);    // rarity / 2
        }

        [Theory]
        [InlineData(46, 9)]
        [InlineData(41, 8)]
        [InlineData(23, 5)]
        [InlineData(12, 2)]
        [InlineData(2, 1)]
        [InlineData(0, 1)]
        public void ToItem_AttackBonusIsBaseAttackDividedByFive_AtLeastOne(int baseAttack, int expected)
        {
            // Arrange
            var weapon = RemoteSamples.SkywardBlade() with { BaseAttack = baseAttack };

            // Act
            var item = NewImporter().ToItem(weapon);

            // Assert
            Assert.Equal(expected, item.AttackBonus);
        }

        [Fact]
        public void ToItem_LowRarity_GetsSmallerBonuses()
        {
            // Arrange
            var weapon = RemoteSamples.SkywardBlade() with { Rarity = 3 };

            // Act
            var item = NewImporter().ToItem(weapon);

            // Assert
            Assert.Equal(6, item.HealthBonus);
            Assert.Equal(1, item.ArmorBonus);
        }

        [Fact]
        public void ToItem_DescriptionSummarisesTheWeaponOnOneLine()
        {
            // Act
            var item = NewImporter().ToItem(RemoteSamples.SkywardBlade());

            // Assert
            Assert.Contains("Sword", item.Description);
            Assert.Contains("rarity 5", item.Description);
            Assert.Contains("Energy Recharge", item.Description);
            Assert.Contains("Sky-Piercing Fang", item.Description);
            Assert.DoesNotContain("\n", item.Description);
        }

        // ---------- registration ----------

        [Fact]
        public void ImportCharacter_RegistersCharacterAndAbilities()
        {
            // Arrange
            var state = new GameState();

            // Act
            var result = NewImporter().ImportCharacter(RemoteSamples.Albedo(), state);

            // Assert
            Assert.Equal(1, result.CharacterId);
            Assert.Equal(1, result.UniqueAbilityId);
            Assert.Equal(new[] { 2, 3 }, result.LearnedAbilityIds);
            Assert.Equal(1, state.Characters.Count);
            Assert.Equal(3, state.Abilities.Count);
            Assert.Same(result.Character, state.Characters.TryGet(1, out var stored) ? stored : null);
        }

        [Fact]
        public void ImportCharacter_Twice_IsRejectedAndStateIsKept()
        {
            // Arrange
            var state = new GameState();
            var importer = NewImporter();
            importer.ImportCharacter(RemoteSamples.Albedo(), state);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => importer.ImportCharacter(RemoteSamples.Albedo(), state));
            Assert.Equal(1, state.Characters.Count);
            Assert.Equal(3, state.Abilities.Count);
        }

        [Fact]
        public void ImportWeapon_RegistersItem()
        {
            // Arrange
            var state = new GameState();

            // Act
            var result = NewImporter().ImportWeapon(RemoteSamples.SkywardBlade(), state);

            // Assert
            Assert.Equal(1, result.ItemId);
            Assert.Equal(1, state.Items.Count);
        }

        [Fact]
        public void ImportWeapon_Twice_IsRejected()
        {
            // Arrange
            var state = new GameState();
            var importer = NewImporter();
            importer.ImportWeapon(RemoteSamples.SkywardBlade(), state);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => importer.ImportWeapon(RemoteSamples.SkywardBlade(), state));
        }

        [Fact]
        public void ImportedWeapon_CanBeEquippedOnImportedCharacter()
        {
            // Arrange
            var state = new GameState();
            var importer = NewImporter();
            var character = importer.ImportCharacter(RemoteSamples.Albedo(), state).Character;
            var item = importer.ImportWeapon(RemoteSamples.SkywardBlade(), state).Item;
            int attackBefore = character.AttackPower;

            // Act
            character.Equip(item, _logger);

            // Assert
            Assert.Same(item, character.EquippedItem);
            Assert.Equal(attackBefore + 9, character.AttackPower);
        }
    }

    public class RemotePersistenceTests : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"oop-remote-{Guid.NewGuid():N}.json");

        public void Dispose()
        {
            if (File.Exists(_path))
                File.Delete(_path);
        }

        [Fact]
        public void ImportedCharacter_SurvivesSaveToJsonFileAndLoad()
        {
            // Arrange
            var state = new GameState();
            var importer = new RemoteImporter(new StatGenerator(new Random(3)));
            var imported = importer.ImportCharacter(RemoteSamples.Albedo(), state).Character;
            var weapon = importer.ImportWeapon(RemoteSamples.SkywardBlade(), state).Item;
            imported.Equip(weapon, Mock.Of<IGameLogger>());

            var repository = new Repository();
            repository.CharacterSaves["remote"] = CharacterSaveMapper.ToSave("remote", state);
            var storage = new JsonRepositoryStorage(_path);

            // Act
            storage.Save(repository);
            var restored = CharacterSaveMapper.ToGameState(storage.Load().CharacterSaves["remote"]);

            // Assert
            Assert.True(restored.Characters.TryGet(1, out var copy));
            var api = Assert.IsType<ApiCharacter>(copy);
            Assert.Equal(imported.Health, api.Health);
            Assert.Equal(imported.MaxHealth, api.MaxHealth);
            Assert.Equal(imported.Armor, api.Armor);
            Assert.Equal(imported.AttackPower, api.AttackPower);
            Assert.Equal("Geo", api.Profile.Vision);
            Assert.Equal("albedo", api.Profile.RemoteId);
            Assert.Equal(imported.UniqueAbility.Name, api.UniqueAbility.Name);
            Assert.Equal(1, restored.Abilities.IdOf(api.UniqueAbility));
            Assert.Equal(2, api.LearnedAbilities.Count);
            Assert.Equal(weapon.Description, api.EquippedItem!.Description);

            var originalBurst = Assert.IsType<CustomAbility>(imported.UniqueAbility);
            var restoredBurst = Assert.IsType<CustomAbility>(api.UniqueAbility);
            Assert.Equal(originalBurst.Description, restoredBurst.Description);
            Assert.Equal(originalBurst.Hits, restoredBurst.Hits);
        }

        [Fact]
        public void RemoteCharacterWithoutItsUniqueAbilityData_IsRejected()
        {
            // Arrange
            var profile = new ProfileData("albedo", "", "Geo", "Sword", "", "", "", 5);
            var save = new CharacterSave();
            save.Characters.Add(new CharacterData(1, "Albedo", "genshin", 100, 100, 7, 20, 9, profile));

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => CharacterSaveMapper.ToGameState(save));
        }
    }

    public class DescriptionFieldTests
    {
        [Fact]
        public void Item_DescriptionDefaultsToEmpty()
        {
            // Act & Assert
            Assert.Equal(string.Empty, new Item("Axe").Description);
            Assert.Equal("sharp", new Item("Axe", description: "sharp").Description);
        }

        [Fact]
        public void CustomAbility_DescriptionDefaultsToEmpty()
        {
            // Act & Assert
            Assert.Equal(string.Empty, new CustomAbility("A", 1, 0).Description);
            Assert.Equal("text", new CustomAbility("A", 1, 0, "text").Description);
        }
    }
}