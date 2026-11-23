using System;
using GameSystem.Core.Interfaces;
using GameSystem.Core.Logging;
using GameSystem.Core.Models;
using GameSystem.Engine;
using Infra.Display;
using Moq;
using Xunit;

namespace ProjectTests.CharacterTests
{
    public class GameTests
    {
        private readonly Mock<IGameLogger> _logger = new Mock<IGameLogger>();

        [Fact]
        public void Constructor_NullLogger_Throws()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new Game(null!));
        }

        [Fact]
        public void AddCharacter_RegistersAndLogs()
        {
            // Arrange
            var game = new Game(_logger.Object);
            var barbarian = new HalfOrcBarbarian("Growler");

            // Act
            game.AddCharacter(barbarian);

            // Assert
            Assert.Same(barbarian, Assert.Single(game.Participants));
            _logger.Verify(l => l.Log("Character registered: Growler"), Times.Once());
        }

        [Fact]
        public void StartRound_IncrementsRoundAndResetsDefense()
        {
            // Arrange
            var game = new Game(_logger.Object);
            var monk = new Monk("Shi Fuu");
            game.AddCharacter(monk);
            game.StartRound();
            game.ExecuteDefend(monk);

            // Act
            game.StartRound();

            // Assert
            Assert.Equal(2, game.CurrentRound);
            Assert.False(monk.IsDefending);
        }

        [Fact]
        public void ExecuteDefend_PutsCharacterIntoDefense()
        {
            // Arrange
            var game = new Game(_logger.Object);
            var monk = new Monk("Shi Fuu");

            // Act
            game.ExecuteDefend(monk);

            // Assert
            Assert.True(monk.IsDefending);
            _logger.Verify(l => l.Log("[Action] Shi Fuu turn:"), Times.Once());
        }

        [Fact]
        public void ExecuteAttack_DamagesTarget()
        {
            // Arrange
            var game = new Game(_logger.Object);
            var barbarian = new HalfOrcBarbarian("Growler");
            var monk = new Monk("Shi Fuu");

            // Act
            game.ExecuteAttack(barbarian, monk);

            // Assert
            Assert.Equal(78, monk.Health);
            _logger.Verify(l => l.Log("[Action] Growler turn:"), Times.Once());
        }

        [Fact]
        public void ExecuteAbility_UsesUniqueAbility()
        {
            // Arrange
            var game = new Game(_logger.Object);
            var barbarian = new HalfOrcBarbarian("Growler");
            var monk = new Monk("Shi Fuu");

            // Act
            game.ExecuteAbility(barbarian, monk);

            // Assert
            Assert.Equal(68, monk.Health);
        }

        [Fact]
        public void ExecuteHeal_HealsCharacter()
        {
            // Arrange
            var game = new Game(_logger.Object);
            var barbarian = new HalfOrcBarbarian("Growler");
            var monk = new Monk("Shi Fuu");
            barbarian.Attack(monk, _logger.Object); // 78

            // Act
            game.ExecuteHeal(monk, 5);

            // Assert
            Assert.Equal(83, monk.Health);
        }
    }

    public class DisplayerLoggerTests
    {
        [Fact]
        public void Log_ForwardsMessageToDisplayer()
        {
            // Arrange
            var displayer = new Mock<IDisplayer>();
            var logger = new DisplayerLogger(displayer.Object);

            // Act
            logger.Log("hello");

            // Assert
            displayer.Verify(d => d.Display("hello"), Times.Once());
            displayer.Verify(d => d.Display(It.IsAny<IDisplayable>()), Times.Never());
        }
    }
}