using System;
using System.Collections.Generic;
using GameSystem.Core.Abilities;
using GameSystem.Core.Interfaces;
using GameSystem.Core.Mapping;
using GameSystem.Core.Models;
using GameSystem.Core.Remote;
using Infra.Data;
using Moq;
using ProjectTests.CharacterTests;
using ProjectTests.Mocks;
using Xunit;

namespace ProjectTests.CombatTests
{
    public class DamageCalculatorTests
    {
        private static int Calc(int attack, int armor, double resist = 0.0, bool defending = false,
                                DamageKind kind = DamageKind.Physical, double penetration = 0.0) =>
            DamageCalculator.Calculate(attack, armor, resist, defending, new AttackProfile(kind, penetration), GameRules.Default);

        [Theory]
        [InlineData(20, 8, 0.0, false, 12)]   // attack - armor
        [InlineData(20, 8, 0.25, false, 9)]   // 12 x 0.75
        [InlineData(20, 5, 0.0, true, 10)]    // a defending target has double armor: 20 - 10
        [InlineData(15, 16, 0.0, false, 1)]   // armor is bigger than the attack: chip damage
        [InlineData(15, 100, 0.5, false, 1)]  // never less than the minimum
        [InlineData(20, 8, 1.5, false, 1)]    // resist is capped at 90%: 12 x 0.1 = 1.2
        public void Physical_DamageIsAttackMinusArmorReducedByResist(int attack, int armor, double resist, bool defending, int expected)
        {
            // Act & Assert
            Assert.Equal(expected, Calc(attack, armor, resist, defending));
        }

        [Fact]
        public void Physical_ResistPenetrationLowersTheEffectiveResist()
        {
            // Arrange: resist 0.5 - 0.3 = 0.2, so 12 x 0.8 = 9.6
            // Act & Assert
            Assert.Equal(10, Calc(20, 8, resist: 0.5, penetration: 0.3));
        }

        [Theory]
        [InlineData(20, 8, 0.0, false, 0.0, 15)]    // armor is ignored: 20 x 0.75
        [InlineData(20, 100, 0.0, false, 0.0, 15)]  // however big the armor is
        [InlineData(20, 8, 0.2, false, 0.0, 12)]    // 15 x 0.8
        [InlineData(20, 8, 0.0, true, 0.0, 11)]     // a defending target gets +25% resist: 15 x 0.75 = 11.25
        [InlineData(20, 8, 0.3, false, 0.1, 12)]    // resist 0.3 - 0.1 = 0.2
        [InlineData(1, 0, 0.0, false, 0.0, 1)]      // never less than the minimum
        public void Magic_IgnoresArmorAndFeelsResist(int attack, int armor, double resist, bool defending, double penetration, int expected)
        {
            // Act & Assert
            Assert.Equal(expected, Calc(attack, armor, resist, defending, DamageKind.Magic, penetration));
        }

        [Fact]
        public void TheRulesCanLetArmorBlockAHitCompletely()
        {
            // Arrange
            var rules = new GameRules { MinimumDamage = 0 };

            // Act
            int damage = DamageCalculator.Calculate(10, 20, 0.0, false, AttackProfile.Physical, rules);

            // Assert
            Assert.Equal(0, damage);
        }

        [Fact]
        public void Calculate_NullArguments_Throw()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => DamageCalculator.Calculate(1, 1, 0, false, null!, GameRules.Default));
            Assert.Throws<ArgumentNullException>(() => DamageCalculator.Calculate(1, 1, 0, false, AttackProfile.Physical, null!));
        }

        [Fact]
        public void TheDefaultRules_AreTheDocumentedOnes()
        {
            // Act
            var rules = GameRules.Default;

            // Assert
            Assert.Equal(1, rules.MinimumDamage);
            Assert.Equal(2.0, rules.DefenseArmorMultiplier);
            Assert.Equal(0.25, rules.DefenseResistBonus);
            Assert.Equal(0.9, rules.MaxResist);
            Assert.Equal(0.75, rules.MagicScale);
            Assert.Equal(100, rules.BaseSpeed);
        }
    }

    public class CharacterCombatTests
    {
        private readonly Mock<IGameLogger> _logger = new Mock<IGameLogger>();
        private readonly Character _barbarian = new HalfOrcBarbarian("Growler"); // ATK 20
        private readonly Character _monk = new Monk("Shi Fuu");                  // HP 90, ARM 8

        [Fact]
        public void NewCharacters_HaveNoResistAndNormalSpeed()
        {
            // Assert
            Assert.Equal(0.0, _barbarian.Resist);
            Assert.Equal(100, _barbarian.Speed);
        }

        [Fact]
        public void Resist_ReducesPhysicalDamage()
        {
            // Arrange
            _monk.SetCombatStats(0.25, 100);

            // Act
            _barbarian.Attack(_monk, _logger.Object);

            // Assert
            Assert.Equal(81, _monk.Health); // 12 x 0.75 = 9
            _logger.Verify(l => l.Log("Growler attacks Shi Fuu for 9 damage."), Times.Once());
        }

        [Fact]
        public void MagicAttack_IgnoresArmorAndSaysSo()
        {
            // Act
            _barbarian.Attack(_monk, _logger.Object, new AttackProfile(DamageKind.Magic));

            // Assert
            Assert.Equal(75, _monk.Health);
            _logger.Verify(l => l.Log("Growler hits Shi Fuu with magic for 15 damage."), Times.Once());
        }

        [Fact]
        public void Defense_AddsResistAgainstMagic()
        {
            // Arrange
            _monk.PrepareDefense(_logger.Object);

            // Act
            _barbarian.Attack(_monk, _logger.Object, new AttackProfile(DamageKind.Magic));

            // Assert
            Assert.Equal(79, _monk.Health); // 15 x 0.75 = 11.25
        }

        [Theory]
        [InlineData(-0.1, 100)]
        [InlineData(1.5, 100)]
        [InlineData(0.1, 0)]
        public void SetCombatStats_InvalidValues_Throw(double resist, int speed)
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => _monk.SetCombatStats(resist, speed));
        }

        [Fact]
        public void CustomAbility_Magic_HitsWithMagic()
        {
            // Arrange
            var bolt = new CustomAbility("Bolt", 2, 0, "", DamageKind.Magic);

            // Act
            bolt.Execute(_barbarian, _monk, _logger.Object);

            // Assert
            Assert.Equal(60, _monk.Health); // 2 x 15
            Assert.Equal(20, _barbarian.AttackPower);
            _logger.Verify(l => l.Log("Growler uses Bolt!"), Times.Once());
            _logger.Verify(l => l.Log("Growler hits Shi Fuu with magic for 15 damage."), Times.Exactly(2));
        }

        [Fact]
        public void CustomAbility_DescribesItsKindAndPenetration()
        {
            // Act & Assert
            Assert.Equal("3 attack(s) with +2 ATK while used", new CustomAbility("A", 3, 2).Describe());
            Assert.Equal("3 magic attack(s) with +6 ATK while used, ignores 10% of resist",
                new CustomAbility("B", 3, 6, "", DamageKind.Magic, 0.1).Describe());
        }

        [Fact]
        public void CustomAbility_InvalidPenetration_Throws()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => new CustomAbility("A", 1, 0, "", DamageKind.Magic, 1.5));
        }
    }

    public class GeneratedCombatStatsTests
    {
        [Theory]
        [InlineData(5, 0.05, 0.20, 95, 130)]
        [InlineData(4, 0.00, 0.12, 90, 120)]
        [InlineData(3, 0.00, 0.05, 85, 105)]
        public void Generate_GivesResistAndSpeedInTheRangeOfTheRarity(int rarity, double resistMin, double resistMax, int speedMin, int speedMax)
        {
            // Arrange
            var generator = new StatGenerator(new Random(42));

            // Act & Assert
            for (int i = 0; i < 200; i++)
            {
                var stats = generator.Generate(rarity);
                Assert.InRange(stats.Resist, resistMin, resistMax);
                Assert.InRange(stats.Speed, speedMin, speedMax);
            }
        }

        [Fact]
        public void AnImportedCharacter_HasGeneratedResistAndSpeed()
        {
            // Act
            var character = new RemoteImporter(new StatGenerator(new Random(1))).ToCharacter(RemoteSamples.Albedo());

            // Assert
            Assert.InRange(character.Resist, 0.05, 0.20);
            Assert.InRange(character.Speed, 95, 130);
        }

        [Fact]
        public void ImportedTalents_AreMagicExceptTheNormalAttack()
        {
            // Act
            var character = new RemoteImporter(new StatGenerator(new Random(1))).ToCharacter(RemoteSamples.Albedo());

            // Assert
            var burst = Assert.IsType<CustomAbility>(character.UniqueAbility);
            Assert.Equal(DamageKind.Magic, burst.Kind);
            Assert.Equal(0.10, burst.ResistPenetration);

            var normal = Assert.IsType<CustomAbility>(character.LearnedAbilities[0]);
            Assert.Equal(DamageKind.Physical, normal.Kind);
            var skill = Assert.IsType<CustomAbility>(character.LearnedAbilities[1]);
            Assert.Equal(DamageKind.Magic, skill.Kind);
            Assert.Equal(0.0, skill.ResistPenetration);
        }
    }

    public class CombatPersistenceTests
    {
        [Fact]
        public void ResistSpeedAndAbilityKind_AreSavedAndRestored()
        {
            // Arrange
            var (state, _, monk) = TestWorld.Build();
            monk.SetCombatStats(0.15, 130);
            state.Abilities.Add(new CustomAbility("Bolt", 2, 1, "x", DamageKind.Magic, 0.2));

            // Act
            var save = CharacterSaveMapper.ToSave("s", state);
            var restored = CharacterSaveMapper.ToGameState(save);

            // Assert
            var monkData = save.Characters.Find(c => c.Name == "Shi Fuu")!;
            Assert.Equal(0.15, monkData.Resist);
            Assert.Equal(130, monkData.Speed);
            var boltData = save.Abilities.Find(a => a.Name == "Bolt")!;
            Assert.Equal("Magic", boltData.Kind);
            Assert.Equal(0.2, boltData.ResistPenetration);

            restored.Characters.TryFind("Shi Fuu", out var copy, out _);
            Assert.Equal(0.15, copy!.Resist);
            Assert.Equal(130, copy.Speed);
            restored.Abilities.TryFind("Bolt", out var ability, out _);
            var bolt = Assert.IsType<CustomAbility>(ability);
            Assert.Equal(DamageKind.Magic, bolt.Kind);
            Assert.Equal(0.2, bolt.ResistPenetration);
        }

        [Fact]
        public void AnOldSaveWithoutTheNewFields_GetsTheDefaults()
        {
            // Arrange
            var save = new CharacterSave();
            save.Characters.Add(new CharacterData(1, "Growler", "barbarian", 120, 120, 5, 20, 0));

            // Act
            var state = CharacterSaveMapper.ToGameState(save);

            // Assert
            state.Characters.TryGet(1, out var growler);
            Assert.Equal(0.0, growler!.Resist);
            Assert.Equal(100, growler.Speed);
        }

        [Fact]
        public void AnAbilityWithAnUnknownKind_IsPhysical()
        {
            // Arrange
            var save = new CharacterSave();
            save.Abilities.Add(new AbilityData(1, "custom", "Odd", 1, 0, null, "Strange", 0.0));

            // Act
            var state = CharacterSaveMapper.ToGameState(save);

            // Assert
            state.Abilities.TryGet(1, out var ability);
            Assert.Equal(DamageKind.Physical, Assert.IsType<CustomAbility>(ability).Kind);
        }
    }
}