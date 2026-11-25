using System;
using System.Collections.Generic;
using System.Linq;
using GameSystem.Core.Mapping;
using GameSystem.Core.Models;
using GameSystem.Engine;
using Infra.Data;
using Xunit;

namespace ProjectTests.CliTests
{
    public class GameHistoryAndOrderTests
    {
        private readonly GameCliFixture _f = new GameCliFixture();
        private readonly Character _barbarian;
        private readonly Character _monk;

        public GameHistoryAndOrderTests()
        {
            _barbarian = _f.Add(new HalfOrcBarbarian("Growler"));
            _monk = _f.Add(new Monk("Shi Fuu"));
        }

        private void StartBattle() => _f.Run("game start Growler \"Shi Fuu\"");

        [Fact]
        public void History_WithoutAnyBattle_SaysSo()
        {
            // Act
            var lines = _f.Run("game history");

            // Assert
            Assert.Equal("No battle history yet.", Assert.Single(lines));
        }

        [Fact]
        public void History_ListsTheStepsOfTheBattle()
        {
            // Arrange
            StartBattle();
            _f.Run("game attack \"Shi Fuu\"");
            _f.Run("game attack Growler");

            // Act
            var text = Assert.Single(_f.Run("game history"));

            // Assert
            Assert.StartsWith("=== BATTLE HISTORY ===", text);
            Assert.Contains("#1 (round 1) Growler: attack -> Shi Fuu", text);
            Assert.Contains("Growler attacks Shi Fuu for 12 damage.", text);
            Assert.Contains("#2 (round 1) Shi Fuu: attack -> Growler", text);
        }

        [Fact]
        public void History_AfterTheEnd_ShowsTheLastBattle()
        {
            // Arrange
            _barbarian.ModifyAttackPower(1000);
            StartBattle();
            _f.Run("game attack \"Shi Fuu\"");

            // Act
            var text = Assert.Single(_f.Run("game history"));

            // Assert
            Assert.Contains("#1 (round 1) Growler: attack -> Shi Fuu", text);
            Assert.Contains("Shi Fuu has been defeated!", text);
        }

        [Fact]
        public void History_AfterStop_ShowsTheAbandonedBattle()
        {
            // Arrange
            StartBattle();
            _f.Run("game attack \"Shi Fuu\"");
            _f.Run("game stop");

            // Act
            var text = Assert.Single(_f.Run("game history"));

            // Assert
            Assert.Contains("#1 (round 1) Growler", text);
        }

        [Fact]
        public void Status_ShowsTheOrderOfTheRound()
        {
            // Arrange
            StartBattle();

            // Act
            var lines = _f.Run("game status");

            // Assert
            Assert.Contains("Order: Growler > Shi Fuu", lines[0]);
        }

        [Fact]
        public void AFasterCharacter_StartsFirst()
        {
            // Arrange
            _monk.SetCombatStats(0.0, 150);

            // Act
            var lines = _f.Run("game start Growler \"Shi Fuu\"");

            // Assert
            Assert.Contains("Order: Shi Fuu > Growler", lines[1]);
            Assert.StartsWith("It is Shi Fuu's turn.", lines[2]);
        }
    }

    public class GameSaveLoadTests
    {
        private readonly GameCliFixture _f = new GameCliFixture();
        private readonly Character _monk;

        public GameSaveLoadTests()
        {
            _f.Add(new HalfOrcBarbarian("Growler"));
            _monk = _f.Add(new Monk("Shi Fuu"));
        }

        private void StartBattle() => _f.Run("game start Growler \"Shi Fuu\"");

        [Fact]
        public void Save_StoresTheCharactersAndTheBattle()
        {
            // Arrange
            StartBattle();
            _f.Run("game attack \"Shi Fuu\"");

            // Act
            var lines = _f.Run("game save s1");

            // Assert
            Assert.Equal("Game saved as 's1' (memory). Battle in progress: yes.", Assert.Single(lines));
            var save = _f.Storage.Load().CharacterSaves["s1"];
            Assert.Equal(2, save.Characters.Count);
            Assert.Single(save.Battle!.History);
        }

        [Fact]
        public void Save_WithoutABattle_SavesOnlyTheCharacters()
        {
            // Act
            var lines = _f.Run("game save s1");

            // Assert
            Assert.Equal("Game saved as 's1' (memory). Battle in progress: no.", Assert.Single(lines));
            Assert.Null(_f.Storage.Load().CharacterSaves["s1"].Battle);
        }

        [Fact]
        public void Save_WithoutAName_ShowsUsage()
        {
            // Act
            var lines = _f.Run("game save");

            // Assert
            Assert.Equal("Usage: game save <name>", Assert.Single(lines));
        }

        [Fact]
        public void Load_RestoresTheStateAndTheBattle()
        {
            // Arrange
            StartBattle();
            _f.Run("game attack \"Shi Fuu\"");   // the monk has 78 HP, it is its turn
            _f.Run("game save s1");
            _f.Run("game attack Growler");        // the monk hits back: round 2

            // Act
            var lines = _f.Run("game load s1", "y");
            var status = _f.Run("game status");

            // Assert
            Assert.Equal("Game 's1' loaded: 2 character(s), battle restored (round 1, turn of Shi Fuu).", Assert.Single(lines));
            Assert.StartsWith("=== BATTLE (round 1) ===", status[0]);
            Assert.Contains("HP 78/90", status[0]);
            Assert.Contains("HP 120/120", status[0]);
            Assert.Contains("> Shi Fuu", status[0]);
            Assert.Equal("It is Shi Fuu's turn.", status[1]);
        }

        [Fact]
        public void AfterALoad_TheBattleCanBePlayedOn()
        {
            // Arrange
            StartBattle();
            _f.Run("game attack \"Shi Fuu\"");
            _f.Run("game save s1");
            _f.Run("game load s1", "y");

            // Act
            var lines = _f.Run("game attack Growler");

            // Assert
            Assert.Equal("Shi Fuu attacks Growler for 10 damage.", lines[0]);
            Assert.Contains("--- Round 2 ---", lines);
        }

        [Fact]
        public void Load_OfASaveWithoutABattle_EndsTheRunningOne()
        {
            // Arrange
            _f.Run("game save empty");
            StartBattle();

            // Act
            var lines = _f.Run("game load empty", "y");
            var status = _f.Run("game status");

            // Assert
            Assert.Equal("Game 'empty' loaded: 2 character(s), no battle in progress.", Assert.Single(lines));
            Assert.StartsWith("No battle in progress.", Assert.Single(status));
        }

        [Fact]
        public void Load_Declined_KeepsEverything()
        {
            // Arrange
            StartBattle();
            _f.Run("game save s1");
            _f.Run("game attack \"Shi Fuu\"");

            // Act
            var lines = _f.Run("game load s1", "n");
            var status = _f.Run("game status");

            // Assert
            Assert.Equal("Loading cancelled.", Assert.Single(lines));
            Assert.Contains("HP 78/90", status[0]);
        }

        [Fact]
        public void Load_UnknownName_IsReported()
        {
            // Act
            var lines = _f.Run("game load nope");

            // Assert
            Assert.Equal("Save 'nope' not found. Use 'saves' to list available saves.", Assert.Single(lines));
        }

        [Fact]
        public void Load_WithoutAName_ShowsUsage()
        {
            // Act
            var lines = _f.Run("game load");

            // Assert
            Assert.Equal("Usage: game load <name>", Assert.Single(lines));
        }

        [Fact]
        public void Load_KeepsResistAndSpeed()
        {
            // Arrange
            _monk.SetCombatStats(0.1, 150);
            _f.Run("game save s2");

            // Act
            _f.Run("game load s2", "y");

            // Assert
            Assert.True(_f.Session.State.Characters.TryFind("Shi Fuu", out var monk, out _));
            Assert.Equal(150, monk!.Speed);
            Assert.Equal(0.1, monk.Resist);
        }

        [Fact]
        public void Load_ADamagedBattle_ChangesNothing()
        {
            // Arrange
            var save = CharacterSaveMapper.ToSave("bad", _f.Session.State);
            save.Battle = new BattleStateData(new List<int> { 1, 2 }, 1, new List<int> { 0, 1 }, 5, 0, new List<TurnData>());
            _f.Storage.Load().CharacterSaves["bad"] = save;
            StartBattle();

            // Act
            var lines = _f.Run("game load bad", "y");
            var status = _f.Run("game status");

            // Assert
            Assert.StartsWith("[Error executing command]: The saved battle is not valid:", Assert.Single(lines));
            Assert.StartsWith("=== BATTLE (round 1) ===", status[0]);
        }

        [Fact]
        public void TheSavedGame_IsListedByTheSavesCommand()
        {
            // Arrange
            _f.Run("game save s1");

            // Act
            var lines = _f.Run("saves");

            // Assert
            Assert.Equal("=== SAVED GAME STATES ===", lines[0]);
            Assert.StartsWith("  s1 (", lines[1]);
        }
    }
}