using System;
using GameSystem.Core.Abilities;
using GameSystem.Core.Interfaces;
using GameSystem.Core.Models;
using Moq;
using Xunit;

namespace ProjectTests.CharacterTests
{
    public class AbilityTests
    {
        private readonly Mock<IGameLogger> _logger = new Mock<IGameLogger>();

        [Fact]
        public void Rage_AttacksWithBoostedPower()
        {
            // Arrange
            var barbarian = new HalfOrcBarbarian("Growler");
            var monk = new Monk("Shi Fuu");

            // Act
            barbarian.UseSpecialAbility(monk, _logger.Object);

            // Assert
            Assert.Equal(68, monk.Health); // (20 + 10) - 8 = 22 damage
            _logger.Verify(l => l.Log("Growler enters a Rage! Attack increased, armor temporarily lowered."), Times.Once());
        }

        [Fact]
        public void FlurryOfBlows_AttacksTwice()
        {
            // Arrange
            var monk = new Monk("Shi Fuu");
            var barbarian = new HalfOrcBarbarian("Growler");

            // Act
            monk.UseSpecialAbility(barbarian, _logger.Object);

            // Assert
            Assert.Equal(100, barbarian.Health); // 2 x (15 - 5)
            _logger.Verify(l => l.Log("Shi Fuu attacks Growler for 10 damage."), Times.Exactly(2));
        }

        [Fact]
        public void CustomAbility_AttacksGivenNumberOfTimes_AndRestoresAttackPower()
        {
            // Arrange
            var barbarian = new HalfOrcBarbarian("Growler");
            var monk = new Monk("Shi Fuu");
            var whirlwind = new CustomAbility("Whirlwind", hits: 3, attackBonus: 2);

            // Act
            whirlwind.Execute(barbarian, monk, _logger.Object);

            // Assert
            Assert.Equal(48, monk.Health);       // 3 x (22 - 8)
            Assert.Equal(20, barbarian.AttackPower);
            _logger.Verify(l => l.Log("Growler uses Whirlwind!"), Times.Once());
        }

        [Fact]
        public void CustomAbility_TrimsNameAndDescribesItself()
        {
            // Arrange & Act
            var ability = new CustomAbility("  Whirlwind ", 3, 2);

            // Assert
            Assert.Equal("Whirlwind", ability.Name);
            Assert.Equal(3, ability.Hits);
            Assert.Equal(2, ability.AttackBonus);
            Assert.Equal("3 attack(s) with +2 ATK while used", ability.Describe());
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void CustomAbility_BlankName_Throws(string name)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new CustomAbility(name, 1, 0));
        }

        [Fact]
        public void CustomAbility_ZeroHits_Throws()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => new CustomAbility("A", 0, 0));
        }

        [Fact]
        public void CustomAbility_NegativeBonus_Throws()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => new CustomAbility("A", 1, -1));
        }

        [Fact]
        public void DefaultDescribe_IsTheAbilityName()
        {
            // Arrange
            IAbility rage = new RageAbility();

            // Act & Assert
            Assert.Equal("Rage", rage.Describe());
        }
    }
}