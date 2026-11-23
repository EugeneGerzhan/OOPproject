using System;
using System.Linq;
using GameSystem.Core.Abilities;
using GameSystem.Core.Interfaces;
using GameSystem.Core.Models;
using GameSystem.Engine;
using Moq;
using ProjectTests.Mocks;
using Xunit;

namespace ProjectTests.EngineTests
{
    public class BattleTests
    {
        private readonly Mock<IGameLogger> _logger = new Mock<IGameLogger>();
        private readonly Character _barbarian = new HalfOrcBarbarian("Growler"); // HP 120, ARM 5, ATK 20
        private readonly Character _monk = new Monk("Shi Fuu");                  // HP 90, ARM 8, ATK 15

        private Battle NewBattle() => new Battle(new[] { _barbarian, _monk }, _logger.Object);

        // ---------- creation ----------

        [Fact]
        public void NewBattle_StartsInTheFirstRoundWithTheFirstParticipant()
        {
            // Act
            var battle = NewBattle();

            // Assert
            Assert.Equal(1, battle.Round);
            Assert.Equal(0, battle.Actions);
            Assert.Same(_barbarian, battle.Current);
            Assert.False(battle.IsOver);
            Assert.Null(battle.Winner);
            Assert.Null(battle.Outcome);
            Assert.Equal(2, battle.Participants.Count);
        }

        [Fact]
        public void Constructor_WithOneParticipant_Throws()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new Battle(new[] { _barbarian }, _logger.Object));
        }

        [Fact]
        public void Constructor_WithARepeatedParticipant_Throws()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new Battle(new[] { _barbarian, _barbarian }, _logger.Object));
        }

        [Fact]
        public void Constructor_WithADefeatedParticipant_Throws()
        {
            // Arrange
            _monk.RestoreState(0, 90, 8, 15, null, Array.Empty<IAbility>());

            // Act & Assert
            Assert.Throws<ArgumentException>(() => NewBattle());
        }

        [Fact]
        public void Constructor_NullLogger_Throws()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new Battle(new[] { _barbarian, _monk }, null!));
        }

        [Fact]
        public void Constructor_ClearsOldDefense()
        {
            // Arrange
            _barbarian.PrepareDefense(_logger.Object);

            // Act
            NewBattle();

            // Assert
            Assert.False(_barbarian.IsDefending);
        }

        // ---------- turns and rounds ----------

        [Fact]
        public void Attack_DamagesTheTargetAndPassesTheTurn()
        {
            // Arrange
            var battle = NewBattle();

            // Act
            battle.Attack(_monk);

            // Assert
            Assert.Equal(78, _monk.Health);
            Assert.Same(_monk, battle.Current);
            Assert.Equal(1, battle.Actions);
            _logger.Verify(l => l.Log("Growler attacks Shi Fuu for 12 damage."), Times.Once());
        }

        [Fact]
        public void Round_GrowsWhenEveryoneHasActed()
        {
            // Arrange
            var battle = NewBattle();

            // Act
            battle.Attack(_monk);
            battle.Attack(_barbarian);

            // Assert
            Assert.Equal(2, battle.Round);
            Assert.Same(_barbarian, battle.Current);
            _logger.Verify(l => l.Log("--- Round 2 ---"), Times.Once());
        }

        [Fact]
        public void Defense_LastsUntilTheNextRound()
        {
            // Arrange
            var battle = NewBattle();
            battle.Defend();
            Assert.True(_barbarian.IsDefending);

            // Act
            battle.Attack(_barbarian); // the monk attacks the defending barbarian

            // Assert
            Assert.Equal(115, _barbarian.Health); // 15 - 5*2 = 5 damage
            Assert.False(_barbarian.IsDefending); // a new round has started
            _logger.Verify(l => l.Log("Growler assumes a defensive stance!"), Times.Once());
        }

        [Fact]
        public void Heal_HealsTheChosenCharacter()
        {
            // Arrange
            var battle = NewBattle();
            battle.Attack(_monk); // 78

            // Act
            battle.Heal(_monk, 5);

            // Assert
            Assert.Equal(83, _monk.Health);
            _logger.Verify(l => l.Log("Shi Fuu healed for 5 HP. Current Health: 83/90."), Times.Once());
        }

        [Fact]
        public void UseAbility_WithoutAnAbility_UsesTheUniqueOne()
        {
            // Arrange
            var battle = NewBattle();

            // Act
            battle.UseAbility(_monk);

            // Assert
            Assert.Equal(68, _monk.Health); // Rage: (20 + 10) - 8 = 22 damage
            Assert.Same(_monk, battle.Current);
        }

        [Fact]
        public void UseAbility_WithALearnedAbility_ExecutesIt()
        {
            // Arrange
            var battle = NewBattle();
            var zap = new AbilityMock("Zap");
            _barbarian.LearnAbility(zap, _logger.Object);

            // Act
            battle.UseAbility(_monk, zap);

            // Assert
            Assert.Equal(1, zap.ExecuteCount);
            Assert.Same(_monk, battle.Current);
        }

        // ---------- rules ----------

        [Fact]
        public void UseAbility_UnknownAbility_ThrowsAndKeepsTheTurn()
        {
            // Arrange
            var battle = NewBattle();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => battle.UseAbility(_monk, new AbilityMock("Unknown")));
            Assert.Same(_barbarian, battle.Current);
            Assert.Equal(0, battle.Actions);
        }

        [Fact]
        public void Attack_Self_Throws()
        {
            // Arrange
            var battle = NewBattle();

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => battle.Attack(_barbarian));

            // Assert
            Assert.Equal("Growler cannot target itself with this action.", exception.Message);
        }

        [Fact]
        public void Attack_ACharacterOutsideTheBattle_Throws()
        {
            // Arrange
            var battle = NewBattle();

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => battle.Attack(new Monk("Stranger")));

            // Assert
            Assert.Equal("Stranger is not taking part in this battle.", exception.Message);
        }

        [Fact]
        public void Heal_NonPositiveAmount_Throws()
        {
            // Arrange
            var battle = NewBattle();

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => battle.Heal(_barbarian, 0));
        }

        // ---------- the end ----------

        [Fact]
        public void TheLastCharacterStanding_WinsAndTheBattleEnds()
        {
            // Arrange
            _barbarian.ModifyAttackPower(1000);
            var battle = NewBattle();

            // Act
            battle.Attack(_monk);

            // Assert
            Assert.True(battle.IsOver);
            Assert.Same(_barbarian, battle.Winner);
            var outcome = battle.Outcome!;
            Assert.Equal("Growler", outcome.Winner);
            Assert.Equal(new[] { "Growler", "Shi Fuu" }, outcome.Participants);
            Assert.Equal(1, outcome.Rounds);
            Assert.Equal(1, outcome.Actions);
            _logger.Verify(l => l.Log("Growler wins the battle!"), Times.Once());
        }

        [Fact]
        public void AfterTheEnd_NoActionIsPossible()
        {
            // Arrange
            _barbarian.ModifyAttackPower(1000);
            var battle = NewBattle();
            battle.Attack(_monk);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => battle.Defend());
            Assert.Throws<InvalidOperationException>(() => battle.Attack(_monk));
        }

        [Fact]
        public void ADefeatedParticipant_IsSkippedAndTheRoundContinues()
        {
            // Arrange
            var third = new Monk("Third");
            _barbarian.ModifyAttackPower(1000);
            var battle = new Battle(new Character[] { _barbarian, _monk, third }, _logger.Object);

            // Act
            battle.Attack(_monk); // the monk is defeated, the turn goes to the third character

            // Assert
            Assert.False(battle.IsOver);
            Assert.Same(third, battle.Current);
            Assert.Equal(1, battle.Round);

            battle.Attack(_barbarian);
            Assert.Equal(2, battle.Round);
            Assert.Same(_barbarian, battle.Current);
        }

        [Fact]
        public void Attack_AnAlreadyDefeatedTarget_Throws()
        {
            // Arrange
            var third = new Monk("Third");
            _barbarian.ModifyAttackPower(1000);
            var battle = new Battle(new Character[] { _barbarian, _monk, third }, _logger.Object);
            battle.Attack(_monk);

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => battle.Attack(_monk));

            // Assert
            Assert.Equal("Shi Fuu is already defeated.", exception.Message);
        }

        // ---------- the view ----------

        [Fact]
        public void CreateView_DescribesTheBoardWithoutARenderer()
        {
            // Arrange
            var battle = NewBattle();

            // Act
            var view = battle.CreateView();
            var board = view.Data;

            // Assert
            Assert.False(view.HasRenderer);
            Assert.Equal(1, board.Round);
            Assert.Equal("Growler", board.CurrentName);
            Assert.Equal(2, board.Rows.Count);
            Assert.True(board.Rows[0].IsCurrent);
            Assert.False(board.Rows[1].IsCurrent);
            Assert.Equal(120, board.Rows[0].MaxHealth);
        }

        [Fact]
        public void CreateView_AfterTheEnd_HasNoCurrentCharacter()
        {
            // Arrange
            _barbarian.ModifyAttackPower(1000);
            var battle = NewBattle();
            battle.Attack(_monk);

            // Act
            var board = battle.CreateView().Data;

            // Assert
            Assert.True(board.IsOver);
            Assert.Equal("Growler", board.Winner);
            Assert.Equal(string.Empty, board.CurrentName);
            Assert.DoesNotContain(board.Rows, row => row.IsCurrent);
        }
    }

    public class RosterViewTests
    {
        [Fact]
        public void From_ListsTheCharactersOfTheState()
        {
            // Arrange
            var state = new GameState();
            state.Characters.Add(new HalfOrcBarbarian("Growler"));
            var monk = new Monk("Shi Fuu");
            monk.RestoreState(0, 90, 8, 15, null, Array.Empty<IAbility>());
            state.Characters.Add(monk);

            // Act
            var view = RosterView.From(state);

            // Assert
            Assert.Equal(2, view.Rows.Count);
            Assert.Equal(new RosterRow(1, "Growler", "HalfOrcBarbarian", 120, 120, true), view.Rows[0]);
            Assert.False(view.Rows[1].IsAlive);
        }

        [Fact]
        public void From_AnEmptyState_GivesNoRows()
        {
            // Act & Assert
            Assert.Empty(RosterView.From(new GameState()).Rows);
        }
    }
}