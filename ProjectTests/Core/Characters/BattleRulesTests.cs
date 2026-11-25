using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameSystem.Core.Interfaces;
using GameSystem.Core.Mapping;
using GameSystem.Core.Models;
using GameSystem.Engine;
using Infra.Data;
using Moq;
using Xunit;

namespace ProjectTests.EngineTests
{
    public class BattleOrderTests
    {
        private readonly Mock<IGameLogger> _logger = new Mock<IGameLogger>();

        [Fact]
        public void AFasterCharacter_ActsEarlier()
        {
            // Arrange
            var slow = new HalfOrcBarbarian("Slow");
            var fast = new Monk("Fast");
            fast.SetCombatStats(0.0, 150);

            // Act
            var battle = new Battle(new Character[] { slow, fast }, _logger.Object);

            // Assert
            Assert.Same(fast, battle.Current);
            Assert.Equal(new[] { "Fast", "Slow" }, battle.UpcomingOrder);
        }

        [Fact]
        public void ASlowerCharacter_ActsLater()
        {
            // Arrange
            var slow = new HalfOrcBarbarian("Slow");
            slow.SetCombatStats(0.0, 90);
            var normal = new Monk("Normal");

            // Act
            var battle = new Battle(new Character[] { slow, normal }, _logger.Object);

            // Assert
            Assert.Equal(new[] { "Normal", "Slow" }, battle.UpcomingOrder);
        }

        [Fact]
        public void Speed200_GivesTwoActions_AndSpeed150ActsBeforeSpeed100()
        {
            // Arrange
            var a = new HalfOrcBarbarian("A");
            a.SetCombatStats(0.0, 200);
            var b = new Monk("B");                // speed 100
            var c = new Monk("C");
            c.SetCombatStats(0.0, 150);
            var battle = new Battle(new Character[] { a, b, c }, _logger.Object);

            // Act
            var order = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                order.Add(battle.Current.Name);
                battle.Defend();
            }

            // Assert
            Assert.Equal(new[] { "A", "C", "A", "B" }, order);
            Assert.Equal(2, battle.Round);
            Assert.Equal("A", battle.Current.Name);
        }

        [Fact]
        public void EqualSpeeds_ActInTheOrderOfTheParty()
        {
            // Arrange
            var battle = new Battle(new Character[] { new HalfOrcBarbarian("A"), new Monk("B"), new Monk("C") }, _logger.Object);

            // Assert
            Assert.Equal(new[] { "A", "B", "C" }, battle.UpcomingOrder);
        }

        [Fact]
        public void TheBoardShowsTheOrderOfTheRound()
        {
            // Arrange
            var fast = new Monk("Fast");
            fast.SetCombatStats(0.0, 150);
            var battle = new Battle(new Character[] { new HalfOrcBarbarian("Slow"), fast }, _logger.Object);

            // Act
            var board = battle.CreateView().Data;

            // Assert
            Assert.Equal(new[] { "Fast", "Slow" }, board.Order!);
        }
    }

    public class BattleHistoryTests
    {
        private readonly Mock<IGameLogger> _logger = new Mock<IGameLogger>();
        private readonly Character _barbarian = new HalfOrcBarbarian("Growler");
        private readonly Character _monk = new Monk("Shi Fuu");

        private Battle NewBattle() => new Battle(new[] { _barbarian, _monk }, _logger.Object);

        [Fact]
        public void EveryAction_IsWrittenToTheHistory()
        {
            // Arrange
            var battle = NewBattle();

            // Act
            battle.Attack(_monk);

            // Assert
            var step = Assert.Single(battle.History);
            Assert.Equal(1, step.Number);
            Assert.Equal(1, step.Round);
            Assert.Equal("Growler", step.Actor);
            Assert.Equal("attack", step.Action);
            Assert.Equal("Shi Fuu", step.Target);
            Assert.Equal(new[] { "Growler attacks Shi Fuu for 12 damage." }, step.Messages);
        }

        [Fact]
        public void Defend_HasNoTarget()
        {
            // Arrange
            var battle = NewBattle();

            // Act
            battle.Defend();

            // Assert
            var step = Assert.Single(battle.History);
            Assert.Equal("defend", step.Action);
            Assert.Null(step.Target);
            Assert.Equal(new[] { "Growler assumes a defensive stance!" }, step.Messages);
        }

        [Fact]
        public void Heal_IsRecordedWithItsTarget()
        {
            // Arrange
            var battle = NewBattle();
            battle.Attack(_monk);

            // Act
            battle.Heal(_monk, 5);

            // Assert
            var step = battle.History[1];
            Assert.Equal(2, step.Number);
            Assert.Equal("heal", step.Action);
            Assert.Equal("Shi Fuu", step.Target);
        }

        [Fact]
        public void Ability_IsRecordedWithItsName_AndAllItsMessages()
        {
            // Arrange
            var battle = NewBattle();

            // Act
            battle.UseAbility(_monk);

            // Assert
            var step = Assert.Single(battle.History);
            Assert.Equal("ability Rage", step.Action);
            Assert.Equal(2, step.Messages.Count);
        }

        [Fact]
        public void TheRoundMarker_IsNotPartOfAStep_ButTheRoundNumberIs()
        {
            // Arrange
            var battle = NewBattle();
            battle.Attack(_monk);

            // Act
            battle.Attack(_barbarian);
            battle.Defend();

            // Assert
            Assert.Equal(new[] { "Shi Fuu attacks Growler for 10 damage." }, battle.History[1].Messages);
            Assert.Equal(1, battle.History[1].Round);
            Assert.Equal(2, battle.History[2].Round);
        }

        [Fact]
        public void ARejectedAction_LeavesNoTrace()
        {
            // Arrange
            var battle = NewBattle();

            // Act
            Assert.Throws<InvalidOperationException>(() => battle.Attack(_barbarian));

            // Assert
            Assert.Empty(battle.History);
        }

        [Fact]
        public void TheLastStep_ContainsTheDefeatMessage()
        {
            // Arrange
            _barbarian.ModifyAttackPower(1000);
            var battle = NewBattle();

            // Act
            battle.Attack(_monk);

            // Assert
            Assert.Contains("Shi Fuu has been defeated!", battle.History[0].Messages);
        }
    }

    public class BattleProgressTests
    {
        private readonly Mock<IGameLogger> _logger = new Mock<IGameLogger>();
        private readonly Character _barbarian = new HalfOrcBarbarian("Growler");
        private readonly Character _monk = new Monk("Shi Fuu");

        private Battle NewBattle() => new Battle(new[] { _barbarian, _monk }, _logger.Object);

        [Fact]
        public void GetProgress_DescribesTheStateOfTheBattle()
        {
            // Arrange
            var battle = NewBattle();
            battle.Attack(_monk);

            // Act
            var progress = battle.GetProgress();

            // Assert
            Assert.Equal(1, progress.Round);
            Assert.Equal(1, progress.QueueIndex);
            Assert.Equal(1, progress.Actions);
            Assert.Equal(new[] { 0, 1 }, progress.Queue);
            Assert.Single(progress.History);
        }

        [Fact]
        public void GetProgress_IsACopy()
        {
            // Arrange
            var battle = NewBattle();
            battle.Attack(_monk);
            var progress = battle.GetProgress();

            // Act
            battle.Attack(_barbarian);

            // Assert
            Assert.Single(progress.History);
            Assert.Equal(1, progress.Actions);
        }

        [Fact]
        public void Restore_ContinuesWhereTheBattleStopped()
        {
            // Arrange
            var battle = NewBattle();
            battle.Attack(_monk);
            var progress = battle.GetProgress();

            // Act
            var restored = Battle.Restore(new[] { _barbarian, _monk }, _logger.Object, progress);

            // Assert
            Assert.Same(_monk, restored.Current);
            Assert.Equal(1, restored.Round);
            Assert.Equal(1, restored.Actions);
            Assert.Single(restored.History);

            restored.Attack(_barbarian);
            Assert.Equal(2, restored.Round);
            Assert.Equal(2, restored.History.Count);
            Assert.Equal(2, restored.History[1].Number);
        }

        [Fact]
        public void Restore_KeepsAHalfFinishedRoundWithAnExtraAction()
        {
            // Arrange
            var a = new HalfOrcBarbarian("A");
            a.SetCombatStats(0.0, 200);
            var b = new Monk("B");
            var battle = new Battle(new Character[] { a, b }, _logger.Object);
            battle.Defend(); // the first action of A

            // Act
            var restored = Battle.Restore(new Character[] { a, b }, _logger.Object, battle.GetProgress());

            // Assert
            Assert.Equal(new[] { "A", "B" }, restored.UpcomingOrder);
        }

        [Fact]
        public void Restore_WithAnInvalidTurnOrder_Throws()
        {
            // Arrange
            var progress = new BattleProgress(1, new[] { 0, 1 }, 5, 0, new List<TurnRecord>());

            // Act & Assert
            Assert.Throws<ArgumentException>(() => Battle.Restore(new[] { _barbarian, _monk }, _logger.Object, progress));
        }

        [Fact]
        public void Restore_WithAnUnknownParticipantInTheQueue_Throws()
        {
            // Arrange
            var progress = new BattleProgress(1, new[] { 0, 7 }, 0, 0, new List<TurnRecord>());

            // Act & Assert
            Assert.Throws<ArgumentException>(() => Battle.Restore(new[] { _barbarian, _monk }, _logger.Object, progress));
        }

        [Fact]
        public void Restore_WithOneParticipant_Throws()
        {
            // Arrange
            var progress = new BattleProgress(1, new[] { 0 }, 0, 0, new List<TurnRecord>());

            // Act & Assert
            Assert.Throws<ArgumentException>(() => Battle.Restore(new[] { _barbarian }, _logger.Object, progress));
        }

        [Fact]
        public void Restore_WhenTheCurrentCharacterIsDefeated_Throws()
        {
            // Arrange
            var third = new Monk("Third");
            _barbarian.RestoreState(0, 120, 5, 20, null, Array.Empty<GameSystem.Core.Abilities.IAbility>());
            var progress = new BattleProgress(1, new[] { 0, 1, 2 }, 0, 0, new List<TurnRecord>());

            // Act & Assert
            Assert.Throws<ArgumentException>(() => Battle.Restore(new Character[] { _barbarian, _monk, third }, _logger.Object, progress));
        }

        [Fact]
        public void Restore_WithOneLivingCharacter_Throws()
        {
            // Arrange
            _monk.RestoreState(0, 90, 8, 15, null, Array.Empty<GameSystem.Core.Abilities.IAbility>());
            var progress = new BattleProgress(1, new[] { 0, 1 }, 0, 0, new List<TurnRecord>());

            // Act & Assert
            Assert.Throws<ArgumentException>(() => Battle.Restore(new[] { _barbarian, _monk }, _logger.Object, progress));
        }
    }

    public class BattleSaveMapperTests : IDisposable
    {
        private readonly Mock<IGameLogger> _logger = new Mock<IGameLogger>();
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"oop-battle-{Guid.NewGuid():N}.json");
        private readonly GameState _state = new GameState();
        private readonly Character _barbarian = new HalfOrcBarbarian("Growler");
        private readonly Character _monk = new Monk("Shi Fuu");

        public BattleSaveMapperTests()
        {
            _state.Characters.Add(_barbarian); // id 1
            _state.Characters.Add(_monk);      // id 2
            _state.Abilities.Add(_barbarian.UniqueAbility);
            _state.Abilities.Add(_monk.UniqueAbility);
        }

        public void Dispose()
        {
            if (File.Exists(_path))
                File.Delete(_path);
        }

        private Battle StartedBattle()
        {
            var battle = new Battle(new[] { _barbarian, _monk }, _logger.Object);
            battle.Attack(_monk);
            return battle;
        }

        [Fact]
        public void ToData_SavesTheParticipantsTheTurnOrderAndTheHistory()
        {
            // Arrange
            var battle = StartedBattle();

            // Act
            var data = BattleSaveMapper.ToData(battle, _state);

            // Assert
            Assert.Equal(new[] { 1, 2 }, data.ParticipantIds);
            Assert.Equal(1, data.Round);
            Assert.Equal(new[] { 0, 1 }, data.Queue);
            Assert.Equal(1, data.QueueIndex);
            Assert.Equal(1, data.Actions);
            var step = Assert.Single(data.History);
            Assert.Equal("attack", step.Action);
            Assert.Equal(new[] { "Growler attacks Shi Fuu for 12 damage." }, step.Messages);
        }

        [Fact]
        public void ToData_AParticipantThatIsNotRegistered_Throws()
        {
            // Arrange
            var stranger = new Monk("Stranger");
            var battle = new Battle(new[] { _barbarian, stranger }, _logger.Object);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => BattleSaveMapper.ToData(battle, _state));
        }

        [Fact]
        public void ToBattle_RestoresTheBattleOfTheState()
        {
            // Arrange
            var data = BattleSaveMapper.ToData(StartedBattle(), _state);

            // Act
            var restored = BattleSaveMapper.ToBattle(data, _state, _logger.Object);

            // Assert
            Assert.Same(_monk, restored.Current);
            Assert.Equal(1, restored.Round);
            Assert.Single(restored.History);
        }

        [Fact]
        public void ToBattle_AMissingCharacter_Throws()
        {
            // Arrange
            var data = new BattleStateData(new List<int> { 1, 9 }, 1, new List<int> { 0, 1 }, 0, 0, new List<TurnData>());

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => BattleSaveMapper.ToBattle(data, _state, _logger.Object));
        }

        [Fact]
        public void ToBattle_AnInvalidTurnOrder_ThrowsInvalidData()
        {
            // Arrange
            var data = new BattleStateData(new List<int> { 1, 2 }, 1, new List<int> { 0, 1 }, 8, 0, new List<TurnData>());

            // Act
            var exception = Assert.Throws<InvalidDataException>(() => BattleSaveMapper.ToBattle(data, _state, _logger.Object));

            // Assert
            Assert.Contains("The saved turn order is not valid.", exception.Message);
        }

        [Fact]
        public void TheWholeGame_SurvivesTheJsonFile()
        {
            // Arrange: the battle goes first, so the characters are saved after the hit
            var running = StartedBattle();
            var save = CharacterSaveMapper.ToSave("game", _state);
            save.Battle = BattleSaveMapper.ToData(running, _state);
            var repository = new Repository();
            repository.CharacterSaves["game"] = save;
            var storage = new JsonRepositoryStorage(_path);

            // Act
            storage.Save(repository);
            var loaded = storage.Load().CharacterSaves["game"];
            var state = CharacterSaveMapper.ToGameState(loaded);
            var battle = BattleSaveMapper.ToBattle(loaded.Battle!, state, _logger.Object);

            // Assert
            Assert.Equal("Shi Fuu", battle.Current.Name);
            Assert.Equal(78, battle.Current.Health);
            Assert.Equal(new[] { "Growler attacks Shi Fuu for 12 damage." }, battle.History[0].Messages);
            Assert.True(state.Characters.TryGet(2, out var monk));
            Assert.Same(monk, battle.Current);
        }
    }
}