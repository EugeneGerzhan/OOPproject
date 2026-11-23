using System;
using GameSystem.Core.Abilities;
using GameSystem.Core.Interfaces;
using GameSystem.Core.Models;
using Moq;
using ProjectTests.Mocks;
using Xunit;

namespace ProjectTests.CharacterTests
{
    public class CharacterTests
    {
        private readonly Mock<IGameLogger> _logger = new Mock<IGameLogger>();

        private IGameLogger Log => _logger.Object;

        private static Character NewBarbarian() => new HalfOrcBarbarian("Growler");
        private static Character NewMonk() => new Monk("Shi Fuu");

        // ---------- construction ----------

        [Fact]
        public void Barbarian_HasExpectedBaseStats()
        {
            // Arrange & Act
            var barbarian = NewBarbarian();

            // Assert
            Assert.Equal(120, barbarian.MaxHealth);
            Assert.Equal(120, barbarian.Health);
            Assert.Equal(5, barbarian.Armor);
            Assert.Equal(20, barbarian.AttackPower);
            Assert.IsType<RageAbility>(barbarian.UniqueAbility);
            Assert.True(barbarian.IsAlive);
        }

        [Fact]
        public void Monk_HasExpectedBaseStats()
        {
            // Arrange & Act
            var monk = NewMonk();

            // Assert
            Assert.Equal(90, monk.MaxHealth);
            Assert.Equal(8, monk.Armor);
            Assert.Equal(15, monk.AttackPower);
            Assert.IsType<FlurryOfBlowsAbility>(monk.UniqueAbility);
        }

        [Fact]
        public void Item_StoresBonuses()
        {
            // Arrange & Act
            var item = new Item("Axe", healthBonus: 1, armorBonus: 2, attackBonus: 3);

            // Assert
            Assert.Equal("Axe", item.Name);
            Assert.Equal(1, item.HealthBonus);
            Assert.Equal(2, item.ArmorBonus);
            Assert.Equal(3, item.AttackBonus);
        }

        // ---------- attack ----------

        [Fact]
        public void Attack_DealsAttackMinusArmor()
        {
            // Arrange
            var barbarian = NewBarbarian();
            var monk = NewMonk();

            // Act
            barbarian.Attack(monk, Log);

            // Assert
            Assert.Equal(78, monk.Health);
            _logger.Verify(l => l.Log("Growler attacks Shi Fuu for 12 damage."), Times.Once());
        }

        [Fact]
        public void Attack_AgainstDefendingTarget_DoublesItsArmor()
        {
            // Arrange
            var barbarian = NewBarbarian();
            var monk = NewMonk();
            monk.PrepareDefense(Log);

            // Act
            barbarian.Attack(monk, Log);

            // Assert
            Assert.Equal(86, monk.Health); // 20 - 8*2 = 4 damage
        }

        [Fact]
        public void Attack_DamageIsAtLeastOne()
        {
            // Arrange
            var attacker = NewMonk();
            var target = new Monk("Dummy");
            target.PrepareDefense(Log); // armor 16 > attack 15

            // Act
            attacker.Attack(target, Log);

            // Assert
            Assert.Equal(89, target.Health);
        }

        [Fact]
        public void Attack_LethalDamage_DefeatsTarget()
        {
            // Arrange
            var barbarian = NewBarbarian();
            var monk = NewMonk();
            barbarian.ModifyAttackPower(1000);

            // Act
            barbarian.Attack(monk, Log);

            // Assert
            Assert.False(monk.IsAlive);
            Assert.Equal(0, monk.Health);
            _logger.Verify(l => l.Log("Shi Fuu has been defeated!"), Times.Once());
        }

        [Fact]
        public void Attack_DefeatedAttacker_DoesNothing()
        {
            // Arrange
            var barbarian = NewBarbarian();
            var monk = NewMonk();
            barbarian.ModifyAttackPower(1000);
            barbarian.Attack(monk, Log); // monk is defeated

            // Act
            monk.Attack(barbarian, Log);

            // Assert
            Assert.Equal(120, barbarian.Health);
            _logger.Verify(l => l.Log(It.Is<string>(s => s.StartsWith("Shi Fuu attacks"))), Times.Never());
        }

        [Fact]
        public void Attack_AlreadyDefeatedTarget_IsRejected()
        {
            // Arrange
            var barbarian = NewBarbarian();
            var monk = NewMonk();
            barbarian.ModifyAttackPower(1000);
            barbarian.Attack(monk, Log);

            // Act
            barbarian.Attack(monk, Log);

            // Assert
            _logger.Verify(l => l.Log("Shi Fuu is already defeated."), Times.Once());
            _logger.Verify(l => l.Log("Shi Fuu has been defeated!"), Times.Once());
        }

        // ---------- heal / defense ----------

        [Fact]
        public void Heal_RestoresHealth()
        {
            // Arrange
            var barbarian = NewBarbarian();
            var monk = NewMonk();
            barbarian.Attack(monk, Log); // 78

            // Act
            monk.Heal(5, Log);

            // Assert
            Assert.Equal(83, monk.Health);
        }

        [Fact]
        public void Heal_NeverExceedsMaxHealth()
        {
            // Arrange
            var monk = NewMonk();

            // Act
            monk.Heal(1000, Log);

            // Assert
            Assert.Equal(90, monk.Health);
        }

        [Fact]
        public void Heal_DefeatedCharacter_DoesNothing()
        {
            // Arrange
            var barbarian = NewBarbarian();
            var monk = NewMonk();
            barbarian.ModifyAttackPower(1000);
            barbarian.Attack(monk, Log);

            // Act
            monk.Heal(50, Log);

            // Assert
            Assert.Equal(0, monk.Health);
        }

        [Fact]
        public void Defense_CanBePreparedAndReset()
        {
            // Arrange
            var monk = NewMonk();

            // Act & Assert
            monk.PrepareDefense(Log);
            Assert.True(monk.IsDefending);
            _logger.Verify(l => l.Log("Shi Fuu assumes a defensive stance!"), Times.Once());

            monk.ResetDefense();
            Assert.False(monk.IsDefending);
        }

        // ---------- stat modifiers ----------

        [Fact]
        public void ModifyAttackPower_NeverGoesBelowZero()
        {
            // Arrange
            var barbarian = NewBarbarian();

            // Act
            barbarian.ModifyAttackPower(-100);

            // Assert
            Assert.Equal(0, barbarian.AttackPower);
        }

        [Fact]
        public void ModifyArmor_NeverGoesBelowZero()
        {
            // Arrange
            var barbarian = NewBarbarian();

            // Act
            barbarian.ModifyArmor(-100);

            // Assert
            Assert.Equal(0, barbarian.Armor);
        }

        // ---------- equipment ----------

        [Fact]
        public void Equip_AddsItemBonuses()
        {
            // Arrange
            var barbarian = NewBarbarian();
            var axe = new Item("Axe", healthBonus: 10, armorBonus: 3, attackBonus: 5);

            // Act
            barbarian.Equip(axe, Log);

            // Assert
            Assert.Same(axe, barbarian.EquippedItem);
            Assert.Equal(130, barbarian.MaxHealth);
            Assert.Equal(130, barbarian.Health);
            Assert.Equal(8, barbarian.Armor);
            Assert.Equal(25, barbarian.AttackPower);
            _logger.Verify(l => l.Log("Growler equipped Axe (+5 ATK, +3 ARM, +10 HP)."), Times.Once());
        }

        [Fact]
        public void Equip_SecondItem_ReplacesBonusesOfTheFirst()
        {
            // Arrange
            var barbarian = NewBarbarian();
            var first = new Item("Axe", healthBonus: 10, attackBonus: 5);
            var second = new Item("Club", attackBonus: 3);
            barbarian.Equip(first, Log);

            // Act
            barbarian.Equip(second, Log);

            // Assert
            Assert.Same(second, barbarian.EquippedItem);
            Assert.Equal(120, barbarian.MaxHealth);
            Assert.Equal(120, barbarian.Health);
            Assert.Equal(23, barbarian.AttackPower);
            _logger.Verify(l => l.Log("Growler unequipped Axe."), Times.Once());
        }

        [Fact]
        public void Equip_SameItemTwice_DoesNotStackBonuses()
        {
            // Arrange
            var barbarian = NewBarbarian();
            var axe = new Item("Axe", attackBonus: 5);
            barbarian.Equip(axe, Log);

            // Act
            barbarian.Equip(axe, Log);

            // Assert
            Assert.Equal(25, barbarian.AttackPower);
            _logger.Verify(l => l.Log("Growler already has Axe equipped."), Times.Once());
        }

        // ---------- abilities ----------

        [Fact]
        public void HasAbility_UniqueAbilityIsKnownFromTheStart()
        {
            // Arrange
            var barbarian = NewBarbarian();

            // Act & Assert
            Assert.True(barbarian.HasAbility(barbarian.UniqueAbility));
            Assert.False(barbarian.HasAbility(new AbilityMock()));
        }

        [Fact]
        public void LearnAbility_NewAbility_IsLearnedOnce()
        {
            // Arrange
            var barbarian = NewBarbarian();
            var zap = new AbilityMock("Zap");

            // Act
            bool first = barbarian.LearnAbility(zap, Log);
            bool second = barbarian.LearnAbility(zap, Log);

            // Assert
            Assert.True(first);
            Assert.False(second);
            Assert.Single(barbarian.LearnedAbilities);
            Assert.True(barbarian.HasAbility(zap));
            _logger.Verify(l => l.Log("Growler learned Zap."), Times.Once());
            _logger.Verify(l => l.Log("Growler already knows Zap."), Times.Once());
        }

        [Fact]
        public void LearnAbility_OwnUniqueAbility_IsRejected()
        {
            // Arrange
            var barbarian = NewBarbarian();

            // Act
            bool learned = barbarian.LearnAbility(barbarian.UniqueAbility, Log);

            // Assert
            Assert.False(learned);
            Assert.Empty(barbarian.LearnedAbilities);
        }

        [Fact]
        public void UseAbility_KnownAbility_ExecutesItOnce()
        {
            // Arrange
            var barbarian = NewBarbarian();
            var monk = NewMonk();
            var zap = new AbilityMock("Zap");
            barbarian.LearnAbility(zap, Log);

            // Act
            barbarian.UseAbility(zap, monk, Log);

            // Assert
            Assert.Equal(1, zap.ExecuteCount);
            Assert.Same(barbarian, zap.LastSource);
            Assert.Same(monk, zap.LastTarget);
        }

        [Fact]
        public void UseAbility_UnknownAbility_IsNotExecuted()
        {
            // Arrange
            var barbarian = NewBarbarian();
            var monk = NewMonk();
            var zap = new AbilityMock("Zap");

            // Act
            barbarian.UseAbility(zap, monk, Log);

            // Assert
            Assert.Equal(0, zap.ExecuteCount);
            _logger.Verify(l => l.Log("Growler does not know Zap."), Times.Once());
        }

        [Fact]
        public void UseSpecialAbility_DefeatedCharacter_DoesNothing()
        {
            // Arrange
            var barbarian = NewBarbarian();
            var monk = NewMonk();
            barbarian.ModifyAttackPower(1000);
            barbarian.Attack(monk, Log);

            // Act
            monk.UseSpecialAbility(barbarian, Log);

            // Assert
            Assert.Equal(120, barbarian.Health);
        }

        // ---------- persistence support ----------

        [Fact]
        public void RestoreState_SetsAllValuesDirectly()
        {
            // Arrange
            var barbarian = NewBarbarian();
            var axe = new Item("Axe", attackBonus: 5);
            var zap = new AbilityMock("Zap");

            // Act
            barbarian.RestoreState(50, 140, 9, 33, axe, new IAbility[] { zap });

            // Assert
            Assert.Equal(50, barbarian.Health);
            Assert.Equal(140, barbarian.MaxHealth);
            Assert.Equal(9, barbarian.Armor);
            Assert.Equal(33, barbarian.AttackPower);
            Assert.Same(axe, barbarian.EquippedItem);
            Assert.Same(zap, Assert.Single(barbarian.LearnedAbilities));
        }

        [Fact]
        public void RestoreState_ClampsHealthToMaximum()
        {
            // Arrange
            var barbarian = NewBarbarian();

            // Act
            barbarian.RestoreState(500, 100, 5, 20, null, Array.Empty<IAbility>());

            // Assert
            Assert.Equal(100, barbarian.Health);
        }

        [Fact]
        public void RestoreState_NegativeHealth_MeansDefeated()
        {
            // Arrange
            var barbarian = NewBarbarian();

            // Act
            barbarian.RestoreState(-5, 100, 5, 20, null, Array.Empty<IAbility>());

            // Assert
            Assert.Equal(0, barbarian.Health);
            Assert.False(barbarian.IsAlive);
        }

        [Fact]
        public void RestoreState_ReplacesPreviouslyLearnedAbilities()
        {
            // Arrange
            var barbarian = NewBarbarian();
            barbarian.LearnAbility(new AbilityMock("Old"), Log);

            // Act
            barbarian.RestoreState(100, 100, 5, 20, null, Array.Empty<IAbility>());

            // Assert
            Assert.Empty(barbarian.LearnedAbilities);
            Assert.Null(barbarian.EquippedItem);
        }
    }
}
