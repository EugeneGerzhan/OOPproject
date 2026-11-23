using System;
using System.Collections.Generic;
using System.Linq;
using GameSystem.Core.Abilities;
using GameSystem.Core.Logging;
using GameSystem.Core.Models;
using Infra.Data;
using OOPproject.SOLID;
using ProjectTests.Mocks;
using Xunit;

namespace ProjectTests.CliTests
{
    /// <summary>Characters + game strategies on one command line with a shared game session.</summary>
    public class GameCliFixture
    {
        public DisplayerMock Displayer { get; } = new DisplayerMock();
        public InputReaderStub Input { get; } = new InputReaderStub();
        public InMemoryStorage Storage { get; } = new InMemoryStorage();
        public GameSession Session { get; } = new GameSession();
        public Cli Cli { get; }

        public GameCliFixture()
        {
            var ui = new Presenter(Displayer, Input);
            var saves = new SaveManager(Storage);
            var logger = new DisplayerLogger(Displayer);

            Cli = new Cli(ui);
            Cli.UseStrategy(new CharactersCliHandler(logger, ui, saves, Session));
            Cli.UseStrategy(new GameStrategy(Session, saves, ui, Cli, logger));
        }

        /// <summary>Adds a character straight to the game state (and its unique ability, as 'create char' does).</summary>
        public Character Add(Character character)
        {
            Session.State.Characters.Add(character);
            Session.State.Abilities.Add(character.UniqueAbility);
            return character;
        }

        public IReadOnlyList<string> Run(string command, params string[] answers)
        {
            Displayer.Reset();
            Input.Clear();
            Input.Enqueue(answers);
            Cli.ExecCommand(command);
            return Displayer.Lines.ToList();
        }
    }

    public class GameStrategyTests
    {
        private readonly GameCliFixture _f = new GameCliFixture();
        private readonly Character _barbarian;
        private readonly Character _monk;

        public GameStrategyTests()
        {
            _barbarian = _f.Add(new HalfOrcBarbarian("Growler")); // id 1, HP 120, ARM 5, ATK 20
            _monk = _f.Add(new Monk("Shi Fuu"));                  // id 2, HP 90, ARM 8, ATK 15
        }

        private void StartBattle() => _f.Run("game start Growler \"Shi Fuu\"");

        // ---------- usage and roster ----------

        [Fact]
        public void Game_WithoutArguments_ShowsUsage()
        {
            // Act
            var lines = _f.Run("game");

            // Assert
            Assert.StartsWith("Usage: game", Assert.Single(lines));
        }

        [Fact]
        public void Game_UnknownAction_ShowsUsage()
        {
            // Act
            var lines = _f.Run("game dance");

            // Assert
            Assert.StartsWith("Usage: game", Assert.Single(lines));
        }

        [Fact]
        public void Roster_ListsTheCharactersThatCanBeChosen()
        {
            // Act
            var text = Assert.Single(_f.Run("game roster"));

            // Assert
            Assert.StartsWith("=== AVAILABLE CHARACTERS ===", text);
            Assert.Contains("[1] Growler (HalfOrcBarbarian) HP 120/120", text);
            Assert.Contains("[2] Shi Fuu (Monk) HP 90/90", text);
            Assert.Contains("game start", text);
        }

        [Fact]
        public void Roster_MarksDefeatedCharacters()
        {
            // Arrange
            _monk.RestoreState(0, 90, 8, 15, null, Array.Empty<IAbility>());

            // Act
            var text = Assert.Single(_f.Run("game roster"));

            // Assert
            Assert.Contains("[2] Shi Fuu (Monk) HP 0/90 - defeated", text);
        }

        [Fact]
        public void Roster_WithoutCharacters_SaysHowToGetSome()
        {
            // Arrange
            var empty = new GameCliFixture();

            // Act
            var text = Assert.Single(empty.Run("game roster"));

            // Assert
            Assert.Contains("(none)", text);
        }

        // ---------- starting ----------

        [Fact]
        public void Start_BeginsTheBattleAndShowsTheBoard()
        {
            // Act
            var lines = _f.Run("game start Growler \"Shi Fuu\"");

            // Assert
            Assert.Equal(3, lines.Count);
            Assert.Equal("Battle started: Growler vs Shi Fuu.", lines[0]);
            Assert.StartsWith("=== BATTLE (round 1) ===", lines[1]);
            Assert.Contains("> Growler", lines[1]);
            Assert.Contains("HP 90/90", lines[1]);
            Assert.Contains("Turn: Growler", lines[1]);
            Assert.StartsWith("It is Growler's turn.", lines[2]);
        }

        [Fact]
        public void Start_AcceptsIds()
        {
            // Act
            var lines = _f.Run("game start 2 1");

            // Assert
            Assert.Equal("Battle started: Shi Fuu vs Growler.", lines[0]);
        }

        [Fact]
        public void Start_WithOneParticipant_ShowsUsage()
        {
            // Act
            var lines = _f.Run("game start Growler");

            // Assert
            Assert.Equal("Usage: game start <name|id> <name|id> [...]", Assert.Single(lines));
        }

        [Fact]
        public void Start_UnknownCharacter_IsReported()
        {
            // Act
            var lines = _f.Run("game start Growler Missing");

            // Assert
            Assert.Equal("Character: 'Missing' not found.", Assert.Single(lines));
        }

        [Fact]
        public void Start_WithADefeatedCharacter_IsRejected()
        {
            // Arrange
            _monk.RestoreState(0, 90, 8, 15, null, Array.Empty<IAbility>());

            // Act
            var lines = _f.Run("game start Growler \"Shi Fuu\"");

            // Assert
            Assert.Equal("Defeated characters cannot take part in a battle.", Assert.Single(lines));
        }

        [Fact]
        public void Start_WithTheSameCharacterTwice_IsRejected()
        {
            // Act
            var lines = _f.Run("game start Growler Growler");

            // Assert
            Assert.Equal("A character cannot take part in a battle twice.", Assert.Single(lines));
        }

        [Fact]
        public void Start_WhileABattleGoesOn_IsRejected()
        {
            // Arrange
            StartBattle();

            // Act
            var lines = _f.Run("game start Growler \"Shi Fuu\"");

            // Assert
            Assert.Equal("A battle is already in progress. Use 'game stop' to abandon it.", Assert.Single(lines));
        }

        // ---------- the main loop ----------

        [Fact]
        public void ActionsWithoutABattle_AreRejected()
        {
            // Act
            var lines = _f.Run("game attack Missing");

            // Assert
            Assert.Equal("No battle in progress. Use 'game start <name|id> <name|id> ...'.", Assert.Single(lines));
        }

        [Fact]
        public void Status_WithoutABattle_SaysSo()
        {
            // Act
            var lines = _f.Run("game status");

            // Assert
            Assert.StartsWith("No battle in progress.", Assert.Single(lines));
        }

        [Fact]
        public void Status_ShowsTheBoardAndTheTurn()
        {
            // Arrange
            StartBattle();

            // Act
            var lines = _f.Run("game status");

            // Assert
            Assert.Equal(2, lines.Count);
            Assert.StartsWith("=== BATTLE (round 1) ===", lines[0]);
            Assert.Equal("It is Growler's turn.", lines[1]);
        }

        [Fact]
        public void Attack_HitsTheTargetAndPassesTheTurn()
        {
            // Arrange
            StartBattle();

            // Act
            var lines = _f.Run("game attack \"Shi Fuu\"");

            // Assert
            Assert.Equal(3, lines.Count);
            Assert.Equal("Growler attacks Shi Fuu for 12 damage.", lines[0]);
            Assert.Contains("> Shi Fuu", lines[1]);
            Assert.Contains("HP 78/90", lines[1]);
            Assert.Equal("It is Shi Fuu's turn.", lines[2]);
        }

        [Fact]
        public void Attack_TheNameCanBeTypedWithoutQuotes()
        {
            // Arrange
            StartBattle();

            // Act
            var lines = _f.Run("game attack Shi Fuu");

            // Assert
            Assert.Equal("Growler attacks Shi Fuu for 12 damage.", lines[0]);
        }

        [Fact]
        public void Attack_WithoutATarget_ShowsUsage()
        {
            // Arrange
            StartBattle();

            // Act
            var lines = _f.Run("game attack");

            // Assert
            Assert.Equal("Usage: game attack <target>", Assert.Single(lines));
        }

        [Fact]
        public void Attack_Yourself_IsRejected()
        {
            // Arrange
            StartBattle();

            // Act
            var lines = _f.Run("game attack Growler");

            // Assert
            Assert.Equal("Growler cannot target itself with this action.", Assert.Single(lines));
        }

        [Fact]
        public void Attack_ACharacterOutsideTheBattle_IsRejected()
        {
            // Arrange
            _f.Add(new Monk("Albedo"));
            StartBattle();

            // Act
            var lines = _f.Run("game attack Albedo");

            // Assert
            Assert.Equal("Albedo is not taking part in this battle.", Assert.Single(lines));
        }

        [Fact]
        public void Attack_UnknownTarget_IsReported()
        {
            // Arrange
            StartBattle();

            // Act
            var lines = _f.Run("game attack Missing");

            // Assert
            Assert.Equal("Character: 'Missing' not found.", Assert.Single(lines));
        }

        [Fact]
        public void ARoundEnds_WhenEveryoneHasActed()
        {
            // Arrange
            StartBattle();
            _f.Run("game attack \"Shi Fuu\"");

            // Act
            var lines = _f.Run("game attack Growler");

            // Assert
            Assert.Equal("Shi Fuu attacks Growler for 10 damage.", lines[0]);
            Assert.Contains("--- Round 2 ---", lines);
            Assert.Contains(lines, l => l.StartsWith("=== BATTLE (round 2) ==="));
            Assert.Equal("It is Growler's turn.", lines[^1]);
        }

        [Fact]
        public void Defend_ShowsTheStanceOnTheBoard()
        {
            // Arrange
            StartBattle();

            // Act
            var lines = _f.Run("game defend");

            // Assert
            Assert.Equal("Growler assumes a defensive stance!", lines[0]);
            Assert.Contains("[defending]", lines[1]);
            Assert.Equal("It is Shi Fuu's turn.", lines[2]);
        }

        [Fact]
        public void Heal_WithoutATarget_HealsTheCurrentCharacter()
        {
            // Arrange
            StartBattle();
            _f.Run("game attack \"Shi Fuu\"");

            // Act
            var lines = _f.Run("game heal --amount 5");

            // Assert
            Assert.Equal("Shi Fuu healed for 5 HP. Current Health: 83/90.", lines[0]);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("abc")]
        public void Heal_InvalidAmount_IsRejected(string amount)
        {
            // Arrange
            StartBattle();

            // Act
            var lines = _f.Run($"game heal --amount {amount}");

            // Assert
            Assert.Equal("--amount must be a positive integer.", Assert.Single(lines));
        }

        [Fact]
        public void Ability_WithoutAnId_UsesTheUniqueAbility()
        {
            // Arrange
            StartBattle();

            // Act
            var lines = _f.Run("game ability \"Shi Fuu\"");

            // Assert
            Assert.Equal("Growler enters a Rage! Attack increased, armor temporarily lowered.", lines[0]);
            Assert.Equal("Growler attacks Shi Fuu for 22 damage.", lines[1]);
            Assert.Equal("It is Shi Fuu's turn.", lines[^1]);
        }

        [Fact]
        public void Ability_NotKnownByTheCharacter_IsRejected()
        {
            // Arrange
            StartBattle();

            // Act: ability 2 is the unique ability of the monk
            var lines = _f.Run("game ability \"Shi Fuu\" --id 2");

            // Assert
            Assert.Equal("Growler does not know 'Flurry of Blows'.", Assert.Single(lines));
        }

        [Fact]
        public void Ability_UnknownId_IsReported()
        {
            // Arrange
            StartBattle();

            // Act
            var lines = _f.Run("game ability \"Shi Fuu\" --id Nothing");

            // Assert
            Assert.Equal("Ability: 'Nothing' not found.", Assert.Single(lines));
        }

        // ---------- the end ----------

        [Fact]
        public void TheWinner_IsAnnouncedAndTheResultIsSaved()
        {
            // Arrange
            _barbarian.ModifyAttackPower(1000);
            StartBattle();

            // Act
            var lines = _f.Run("game attack \"Shi Fuu\"");

            // Assert
            Assert.Equal(5, lines.Count);
            Assert.Equal("Growler attacks Shi Fuu for 1012 damage.", lines[0]);
            Assert.Equal("Shi Fuu has been defeated!", lines[1]);
            Assert.Equal("Growler wins the battle!", lines[2]);
            Assert.StartsWith("=== BATTLE OVER (round 1) ===", lines[3]);
            Assert.Contains("Winner: Growler", lines[3]);
            Assert.Equal("=== Winner: Growler after 1 round(s), 1 action(s). Result saved. ===", lines[4]);

            var saved = Assert.Single(_f.Storage.Load().BattleResults);
            Assert.Equal("Growler", saved.Winner);
            Assert.Equal(new[] { "Growler", "Shi Fuu" }, saved.Participants);
            Assert.Equal(1, saved.Rounds);
        }

        [Fact]
        public void AfterTheEnd_ThereIsNoBattleAnyMore()
        {
            // Arrange
            _barbarian.ModifyAttackPower(1000);
            StartBattle();
            _f.Run("game attack \"Shi Fuu\"");

            // Act
            var lines = _f.Run("game status");

            // Assert
            Assert.StartsWith("No battle in progress.", Assert.Single(lines));
        }

        [Fact]
        public void Results_WhenNothingWasPlayed_SaysSo()
        {
            // Act
            var text = Assert.Single(_f.Run("game results"));

            // Assert
            Assert.StartsWith("=== BATTLE RESULTS ===", text);
            Assert.Contains("(no battles have been finished yet)", text);
        }

        [Fact]
        public void Results_ListsTheFinishedBattles()
        {
            // Arrange
            _barbarian.ModifyAttackPower(1000);
            StartBattle();
            _f.Run("game attack \"Shi Fuu\"");

            // Act
            var text = Assert.Single(_f.Run("game results"));

            // Assert
            Assert.Contains("Growler wins (Growler vs Shi Fuu, 1 round(s), 1 action(s))", text);
        }

        // ---------- stopping and loading ----------

        [Fact]
        public void Stop_AbandonsTheBattleWithoutAResult()
        {
            // Arrange
            StartBattle();

            // Act
            var lines = _f.Run("game stop");

            // Assert
            Assert.Equal("Battle abandoned (no result is saved).", Assert.Single(lines));
            Assert.Empty(_f.Storage.Load().BattleResults);
            Assert.StartsWith("No battle in progress.", Assert.Single(_f.Run("game status")));
        }

        [Fact]
        public void Stop_WithoutABattle_SaysSo()
        {
            // Act
            var lines = _f.Run("game stop");

            // Assert
            Assert.Equal("No battle in progress.", Assert.Single(lines));
        }

        [Fact]
        public void LoadingAState_AbandonsTheRunningBattle()
        {
            // Arrange
            _f.Run("save before");
            StartBattle();
            _f.Run("load before", "y");

            // Act
            var lines = _f.Run("game status");

            // Assert
            Assert.Equal("The game state was replaced (load), so the battle was abandoned.", Assert.Single(lines));
        }

        [Fact]
        public void AfterALoad_ANewBattleCanBeStarted()
        {
            // Arrange
            _f.Run("save before");
            StartBattle();
            _f.Run("load before", "y");

            // Act
            var lines = _f.Run("game start Growler \"Shi Fuu\"");

            // Assert
            Assert.Equal("Battle started: Growler vs Shi Fuu.", lines[0]);
        }
    }

    public class RemoteImageLinkTests
    {
        private readonly RemoteCliFixture _f = new RemoteCliFixture();

        [Fact]
        public void RemoteShow_PrintsTheLinkToTheImage()
        {
            // Arrange
            _f.Api.Characters["albedo"] = RemoteSamples.Albedo() with { ImageUrl = "https://example.com/albedo.png" };

            // Act
            var text = Assert.Single(_f.Run("remote show char albedo"));

            // Assert
            Assert.Contains("Image:       https://example.com/albedo.png", text);
        }

        [Fact]
        public void RemoteShow_WithoutALink_PrintsADash()
        {
            // Act
            var text = Assert.Single(_f.Run("remote show char albedo"));

            // Assert
            Assert.Contains("Image:       -", text);
        }
    }
}