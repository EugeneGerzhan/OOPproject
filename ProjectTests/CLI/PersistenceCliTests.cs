using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameSystem.Core.Interfaces;
using GameSystem.Core.Logging;
using GameSystem.Core.Models;
using GameSystem.Core.Remote;
using GameSystem.Engine;
using Infra.Data;
using Infra.WebApi;
using Moq;
using OOPproject.SOLID;
using ProjectTests.Mocks;
using Xunit;

namespace ProjectTests.CliTests
{
    /// <summary>The characters, game and persistence strategies wired like in the program (with the autosave).</summary>
    public class PersistenceFixture
    {
        public DisplayerMock Displayer { get; } = new DisplayerMock();
        public InputReaderStub Input { get; } = new InputReaderStub();
        public InMemoryStorage Storage { get; }
        public GameSession Session { get; } = new GameSession();
        public Cli Cli { get; }
        public GameStrategy Game { get; }
        public AutoSaver AutoSaver { get; }

        /// <param name="storage">A storage of an earlier "run of the program"; null for a first run.</param>
        public PersistenceFixture(InMemoryStorage? storage = null)
        {
            Storage = storage ?? new InMemoryStorage();

            var ui = new Presenter(Displayer, Input);
            var saves = new SaveManager(Storage);
            var logger = new DisplayerLogger(Displayer);

            Cli = new Cli(ui);
            Game = new GameStrategy(Session, saves, ui, Cli, logger);
            AutoSaver = new AutoSaver(Session, saves, () => Game.CurrentBattle);

            Cli.UseStrategy(new CharactersCliHandler(logger, ui, saves, Session));
            Cli.UseStrategy(Game);
            Cli.UseStrategy(new PersistenceStrategy(AutoSaver, saves, ui));
            Cli.AfterCommand = () => AutoSaver.SaveIfChanged();
        }

        public IReadOnlyList<string> Run(string command, params string[] answers)
        {
            Displayer.Reset();
            Input.Clear();
            Input.Enqueue(answers);
            Cli.ExecCommand(command);
            return Displayer.Lines.ToList();
        }

        public void CreateCharacter(string name, string classKey) => Run("create char", name, classKey, "");
    }

    public class AfterCommandHookTests
    {
        private sealed class OneCommand : CommandStrategyBase
        {
            public OneCommand(string selector, Action<string[], Dictionary<string, string>> action)
            {
                Register(selector, action);
            }
        }

        private readonly DisplayerMock _displayer = new DisplayerMock();
        private readonly Cli _cli;
        private int _calls;

        public AfterCommandHookTests()
        {
            _cli = new Cli(new Presenter(_displayer, new InputReaderStub()));
            _cli.UseStrategy(new OneCommand("ok", (a, o) => { }));
            _cli.UseStrategy(new OneCommand("fail", (a, o) => throw new InvalidOperationException("boom")));
            _cli.AfterCommand = () => _calls++;
        }

        [Fact]
        public void TheHook_RunsAfterACommand()
        {
            // Act
            _cli.ExecCommand("ok");

            // Assert
            Assert.Equal(1, _calls);
        }

        [Fact]
        public void TheHook_RunsAfterAFailedCommandToo()
        {
            // Act
            _cli.ExecCommand("fail");

            // Assert
            Assert.Equal(1, _calls);
        }

        [Theory]
        [InlineData("")]
        [InlineData("unknown")]
        public void TheHook_DoesNotRunWhenNoCommandWasRun(string line)
        {
            // Act
            _cli.ExecCommand(line);

            // Assert
            Assert.Equal(0, _calls);
        }

        [Fact]
        public void AFailingHook_IsReportedAndDoesNotBreakTheCommandLine()
        {
            // Arrange
            _cli.AfterCommand = () => throw new InvalidOperationException("disk is full");

            // Act
            _cli.ExecCommand("ok");

            // Assert
            Assert.Equal(
                "[Warning]: the automatic step after the command failed: disk is full",
                Assert.Single(_displayer.Lines));
        }
    }

    public class AutoSaverTests
    {
        private readonly InMemoryStorage _storage = new InMemoryStorage();
        private readonly GameSession _session = new GameSession();
        private readonly SaveManager _saves;
        private readonly Character _barbarian = new HalfOrcBarbarian("Growler");
        private readonly Character _monk = new Monk("Shi Fuu");

        public AutoSaverTests()
        {
            _saves = new SaveManager(_storage);
        }

        private AutoSaver NewAutoSaver(Func<Battle?>? battle = null) => new AutoSaver(_session, _saves, battle ?? (() => null));

        private void AddCharacters()
        {
            _session.State.Characters.Add(_barbarian);
            _session.State.Characters.Add(_monk);
            _session.State.Abilities.Add(_barbarian.UniqueAbility);
            _session.State.Abilities.Add(_monk.UniqueAbility);
        }

        [Fact]
        public void SaveIfChanged_WritesTheAutosaveSlotAndMakesItTheLastSave()
        {
            // Arrange
            AddCharacters();

            // Act
            bool saved = NewAutoSaver().SaveIfChanged();

            // Assert
            Assert.True(saved);
            Assert.True(_saves.TryLoadCharacters(AutoSaver.SlotName, out var save));
            Assert.Equal(2, save!.Characters.Count);
            Assert.Equal(AutoSaver.SlotName, _saves.LastCharacterSaveName);
        }

        [Fact]
        public void AnUnchangedGame_IsNotWrittenAgain()
        {
            // Arrange
            AddCharacters();
            var autoSaver = NewAutoSaver();
            autoSaver.SaveIfChanged();
            int writes = _storage.SaveCalls;

            // Act
            bool saved = autoSaver.SaveIfChanged();

            // Assert
            Assert.False(saved);
            Assert.Equal(writes, _storage.SaveCalls);
        }

        [Fact]
        public void AChange_IsWritten()
        {
            // Arrange
            AddCharacters();
            var autoSaver = NewAutoSaver();
            autoSaver.SaveIfChanged();
            _barbarian.Attack(_monk, Mock.Of<IGameLogger>());

            // Act
            bool saved = autoSaver.SaveIfChanged();

            // Assert
            Assert.True(saved);
            _saves.TryLoadCharacters(AutoSaver.SlotName, out var save);
            Assert.Equal(78, save!.Characters.Single(c => c.Name == "Shi Fuu").Health);
        }

        [Fact]
        public void AnEmptyGame_IsNotSaved()
        {
            // Act
            bool saved = NewAutoSaver().SaveIfChanged();

            // Assert
            Assert.False(saved);
            Assert.False(_saves.TryLoadCharacters(AutoSaver.SlotName, out _));
        }

        [Fact]
        public void WhenDisabled_NothingIsSaved()
        {
            // Arrange
            AddCharacters();
            var autoSaver = NewAutoSaver();
            autoSaver.Enabled = false;

            // Act
            bool saved = autoSaver.SaveIfChanged();

            // Assert
            Assert.False(saved);
            Assert.Null(_saves.LastCharacterSaveName);
        }

        [Fact]
        public void TheBattleInProgress_IsSaved()
        {
            // Arrange
            AddCharacters();
            var battle = new Battle(new[] { _barbarian, _monk }, Mock.Of<IGameLogger>());
            battle.Attack(_monk);

            // Act
            NewAutoSaver(() => battle).SaveIfChanged();

            // Assert
            _saves.TryLoadCharacters(AutoSaver.SlotName, out var save);
            Assert.NotNull(save!.Battle);
            Assert.Single(save.Battle!.History);
        }

        [Fact]
        public void AFinishedBattle_IsNotSaved()
        {
            // Arrange
            AddCharacters();
            _barbarian.ModifyAttackPower(1000);
            var battle = new Battle(new[] { _barbarian, _monk }, Mock.Of<IGameLogger>());
            battle.Attack(_monk);

            // Act
            NewAutoSaver(() => battle).SaveIfChanged();

            // Assert
            _saves.TryLoadCharacters(AutoSaver.SlotName, out var save);
            Assert.Null(save!.Battle);
        }

        [Fact]
        public void TheFirstWriteOfASession_KeepsTheAutosaveOfTheLastSession()
        {
            // Arrange: "the last session" saved the game
            AddCharacters();
            NewAutoSaver().SaveIfChanged();
            _barbarian.Attack(_monk, Mock.Of<IGameLogger>());

            // Act: a new session changes the game
            var next = NewAutoSaver();
            bool saved = next.SaveIfChanged();

            // Assert
            Assert.True(saved);
            _saves.TryLoadCharacters(AutoSaver.PreviousSlotName, out var previous);
            Assert.Equal(90, previous!.Characters.Single(c => c.Name == "Shi Fuu").Health);
            _saves.TryLoadCharacters(AutoSaver.SlotName, out var current);
            Assert.Equal(78, current!.Characters.Single(c => c.Name == "Shi Fuu").Health);
            Assert.Equal(AutoSaver.SlotName, _saves.LastCharacterSaveName);
        }

        [Fact]
        public void ANewSession_ThatChangedNothing_WritesNothingAndKeepsNoPreviousSave()
        {
            // Arrange
            AddCharacters();
            NewAutoSaver().SaveIfChanged();
            int writes = _storage.SaveCalls;

            // Act
            bool saved = NewAutoSaver().SaveIfChanged();

            // Assert
            Assert.False(saved);
            Assert.Equal(writes, _storage.SaveCalls);
            Assert.False(_saves.TryLoadCharacters(AutoSaver.PreviousSlotName, out _));
        }

        [Fact]
        public void Constructor_RejectsNulls()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AutoSaver(null!, _saves, () => null));
            Assert.Throws<ArgumentNullException>(() => new AutoSaver(_session, null!, () => null));
            Assert.Throws<ArgumentNullException>(() => new AutoSaver(_session, _saves, null!));
        }
    }

    public class AutosaveAndContinueCliTests
    {
        private readonly PersistenceFixture _f = new PersistenceFixture();

        [Fact]
        public void AfterACommand_TheCreatedCharacterIsInTheAutosave()
        {
            // Act
            _f.CreateCharacter("Growler", "barbarian");

            // Assert
            var save = _f.Storage.Load().CharacterSaves[AutoSaver.SlotName];
            Assert.Equal("Growler", Assert.Single(save.Characters).Name);
        }

        [Fact]
        public void TheBattle_IsInTheAutosaveToo()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");
            _f.CreateCharacter("Shi Fuu", "monk");

            // Act
            _f.Run("game start Growler \"Shi Fuu\"");
            _f.Run("game attack \"Shi Fuu\"");

            // Assert
            var save = _f.Storage.Load().CharacterSaves[AutoSaver.SlotName];
            Assert.Single(save.Battle!.History);
        }

        [Fact]
        public void TheGame_ContinuesFromTheAutosave_AfterARestart()
        {
            // Arrange: the first run of the program
            _f.CreateCharacter("Growler", "barbarian");
            _f.CreateCharacter("Shi Fuu", "monk");
            _f.Run("game start Growler \"Shi Fuu\"");
            _f.Run("game attack \"Shi Fuu\"");

            // Act: the second run starts from the same file
            var second = new PersistenceFixture(_f.Storage);
            bool continued = second.Game.ContinueFromLastSave(askConfirmation: false);
            var status = second.Run("game status");

            // Assert
            Assert.True(continued);
            Assert.StartsWith("=== BATTLE (round 1) ===", status[0]);
            Assert.Contains("HP 78/90", status[0]);
            Assert.Contains("> Shi Fuu", status[0]);
            Assert.Equal("It is Shi Fuu's turn.", status[1]);
            Assert.False(second.Storage.Load().CharacterSaves.ContainsKey(AutoSaver.PreviousSlotName));
        }

        [Fact]
        public void TheCreatedCharacters_ComeBackAfterARestart()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");
            var second = new PersistenceFixture(_f.Storage);

            // Act
            second.Game.ContinueFromLastSave(askConfirmation: false);
            var roster = Assert.Single(second.Run("game roster"));

            // Assert
            Assert.Contains("[1] Growler (HalfOrcBarbarian) HP 120/120", roster);
        }

        [Fact]
        public void Continue_WithoutAnySave_SaysSo()
        {
            // Act
            var lines = _f.Run("game continue");

            // Assert
            Assert.Equal("There is no save to continue from.", Assert.Single(lines));
        }

        [Fact]
        public void Continue_TakesTheMostRecentSave()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");
            _f.CreateCharacter("Shi Fuu", "monk");
            _f.Run("game save mine");
            _f.CreateCharacter("Albedo", "monk"); // the autosave is newer than 'mine' now

            // Act
            var lines = _f.Run("game continue", "y");

            // Assert
            Assert.Equal("Game 'autosave' loaded: 3 character(s), no battle in progress.", Assert.Single(lines));
        }

        [Fact]
        public void Continue_AfterAnExplicitSave_UsesThatSave()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");
            _f.CreateCharacter("Shi Fuu", "monk");
            _f.Run("game save mine");
            _f.Storage.Load().LastCharacterSave = "mine"; // the pointer names the explicit save

            // Act
            var lines = _f.Run("game continue", "y");

            // Assert
            Assert.Equal("Game 'mine' loaded: 2 character(s), no battle in progress.", Assert.Single(lines));
        }

        [Fact]
        public void Continue_Declined_ChangesNothing()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");
            _f.Run("game save s1");
            _f.CreateCharacter("Shi Fuu", "monk");

            // Act
            var lines = _f.Run("game continue", "n");

            // Assert
            Assert.Equal("Loading cancelled.", Assert.Single(lines));
            Assert.Equal(2, _f.Session.State.Characters.Count);
        }

        // ---------- the commands autosave and storage ----------

        [Fact]
        public void Autosave_ShowsItsState()
        {
            // Act
            var lines = _f.Run("autosave");

            // Assert
            Assert.Equal("Autosave is on (slot 'autosave').", Assert.Single(lines));
        }

        [Fact]
        public void AutosaveOff_StopsTheAutomaticSaving_AndOnStartsItAgain()
        {
            // Arrange
            Assert.Equal("Autosave is off.", Assert.Single(_f.Run("autosave off")));

            // Act
            _f.CreateCharacter("Growler", "barbarian");
            bool savedWhileOff = _f.Storage.Load().CharacterSaves.ContainsKey(AutoSaver.SlotName);
            Assert.Equal("Autosave is on.", Assert.Single(_f.Run("autosave on")));
            _f.CreateCharacter("Shi Fuu", "monk");

            // Assert
            Assert.False(savedWhileOff);
            Assert.True(_f.Storage.Load().CharacterSaves.ContainsKey(AutoSaver.SlotName));
        }

        [Fact]
        public void Autosave_InvalidArgument_ShowsUsage()
        {
            // Act
            var lines = _f.Run("autosave maybe");

            // Assert
            Assert.Equal("Usage: autosave [on|off]", Assert.Single(lines));
        }

        [Fact]
        public void Storage_DescribesWhereTheDataLive()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");

            // Act
            var lines = _f.Run("storage");

            // Assert
            Assert.Equal(5, lines.Count);
            Assert.Equal("=== STORAGE ===", lines[0]);
            Assert.Equal("  Saves file:  memory", lines[1]);
            Assert.Equal("  Saved games: 1, documents: 0, battle results: 0", lines[2]);
            Assert.Equal("  Last save:   autosave", lines[3]);
            Assert.Equal("  Autosave:    on (slot 'autosave')", lines[4]);
        }

        [Fact]
        public void Storage_WithoutAnySave_SaysNone()
        {
            // Act
            var lines = _f.Run("storage");

            // Assert
            Assert.Equal("  Last save:   (none)", lines[3]);
        }
    }

    /// <summary>The remote strategy with a picture cache in a temporary folder.</summary>
    public class PortraitCliFixture : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), $"oop-portraits-{Guid.NewGuid():N}");

        public DisplayerMock Displayer { get; } = new DisplayerMock();
        public InputReaderStub Input { get; } = new InputReaderStub();
        public FakeGameDataApi Api { get; } = new FakeGameDataApi();
        public FakeImageSource Source { get; } = new FakeImageSource();
        public ImageCache Images { get; }
        public Cli Cli { get; }

        public PortraitCliFixture()
        {
            Api.Characters["albedo"] = RemoteSamples.Albedo() with { ImageUrl = "https://example.com/albedo.png" };
            Api.Characters["amber"] = RemoteSamples.Simple("amber", "Amber") with { ImageUrl = "https://example.com/amber.png" };
            Api.Characters["bennett"] = RemoteSamples.Simple("bennett", "Bennett") with { ImageUrl = "https://example.com/bennett.png" };

            Images = new ImageCache(_folder, Source);

            var ui = new Presenter(Displayer, Input);
            Cli = new Cli(ui);
            Cli.UseStrategy(new RemoteCliHandler(
                Api, new RemoteImporter(new StatGenerator(new Random(7))), new GameSession(), ui, new CharacterSheetBuilder(), Images));
        }

        public void Dispose()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        public IReadOnlyList<string> Run(string command)
        {
            Displayer.Reset();
            Cli.ExecCommand(command);
            return Displayer.Lines.ToList();
        }
    }

    public class PortraitCliTests : IDisposable
    {
        private readonly PortraitCliFixture _f = new PortraitCliFixture();

        public void Dispose() => _f.Dispose();

        private string Expected(string name, string key, string source) =>
            $"Portrait of {name}: {Path.Combine(_f.Images.Folder, key + ".png")} ({PngBytes.Pixel.Length} bytes, {source}).";

        [Fact]
        public void Portrait_DownloadsThePictureIntoALocalFile()
        {
            // Act
            var lines = _f.Run("remote portrait albedo");

            // Assert
            Assert.Equal(Expected("Albedo", "albedo-portrait", "downloaded"), Assert.Single(lines));
            Assert.True(File.Exists(Path.Combine(_f.Images.Folder, "albedo-portrait.png")));
            Assert.Equal(new[] { "https://example.com/albedo.png" }, _f.Source.Urls);
        }

        [Fact]
        public void Portrait_TheSecondTime_ComesFromTheCache()
        {
            // Arrange
            _f.Run("remote portrait albedo");

            // Act
            var lines = _f.Run("remote portrait albedo");

            // Assert
            Assert.Equal(Expected("Albedo", "albedo-portrait", "from the cache"), Assert.Single(lines));
            Assert.Equal(1, _f.Source.Calls);
        }

        [Fact]
        public void Portrait_WithoutAnId_ShowsUsage()
        {
            // Act
            var lines = _f.Run("remote portrait");

            // Assert
            Assert.Equal("Usage: remote portrait <id>", Assert.Single(lines));
        }

        [Fact]
        public void Portrait_UnknownCharacter_IsReported()
        {
            // Act
            var lines = _f.Run("remote portrait nobody");

            // Assert
            Assert.Equal("[Error executing command]: Character 'nobody' was not found in the remote database.", Assert.Single(lines));
        }

        [Fact]
        public void Portrait_ACharacterWithoutALink_IsReported()
        {
            // Arrange
            _f.Api.Characters["x"] = RemoteSamples.Simple("x", "X");

            // Act
            var lines = _f.Run("remote portrait x");

            // Assert
            Assert.Equal("[Error executing command]: X has no link to a picture.", Assert.Single(lines));
        }

        [Fact]
        public void Portrait_WhenTheServiceIsDown_IsReported()
        {
            // Arrange
            _f.Source.Failure = new RemoteApiException("Cannot reach the remote service: down");

            // Act
            var lines = _f.Run("remote portrait albedo");

            // Assert
            Assert.Equal("[Error executing command]: Cannot reach the remote service: down", Assert.Single(lines));
        }

        [Fact]
        public void Portraits_DownloadsSeveralPicturesAtOnce()
        {
            // Act
            var lines = _f.Run("remote portraits");

            // Assert
            Assert.Equal($"Portraits: 3 downloaded, 0 taken from the cache, 0 failed (folder: {_f.Images.Folder}).", Assert.Single(lines));
            Assert.Equal(3, _f.Images.List().Count);
        }

        [Fact]
        public void Portraits_TheSecondTime_UsesTheCache()
        {
            // Arrange
            _f.Run("remote portraits");

            // Act
            var lines = _f.Run("remote portraits");

            // Assert
            Assert.Equal($"Portraits: 0 downloaded, 3 taken from the cache, 0 failed (folder: {_f.Images.Folder}).", Assert.Single(lines));
            Assert.Equal(3, _f.Source.Calls);
        }

        [Fact]
        public void Portraits_Find_ChoosesCharacters()
        {
            // Act
            var lines = _f.Run("remote portraits --find alb");

            // Assert
            Assert.StartsWith("Portraits: 1 downloaded", Assert.Single(lines));
        }

        [Fact]
        public void Portraits_Limit_StopsEarly()
        {
            // Act
            var lines = _f.Run("remote portraits --limit 2");

            // Assert
            Assert.StartsWith("Portraits: 2 downloaded", Assert.Single(lines));
        }

        [Theory]
        [InlineData("0")]
        [InlineData("abc")]
        public void Portraits_InvalidLimit_IsRejected(string limit)
        {
            // Act
            var lines = _f.Run($"remote portraits --limit {limit}");

            // Assert
            Assert.Equal("--limit must be a positive integer.", Assert.Single(lines));
        }

        [Fact]
        public void Portraits_NamesTheOnesThatFailed()
        {
            // Arrange
            _f.Api.Characters["x"] = RemoteSamples.Simple("x", "X"); // no link

            // Act
            var lines = _f.Run("remote portraits");

            // Assert
            Assert.StartsWith("Portraits: 3 downloaded, 0 taken from the cache, 1 failed", lines[0]);
            Assert.Equal("  Failed: x", lines[1]);
        }

        [Fact]
        public void Portraits_WhenTheServiceIsDown_AllFail()
        {
            // Arrange
            _f.Source.Failure = new RemoteApiException("down");

            // Act
            var lines = _f.Run("remote portraits");

            // Assert
            Assert.StartsWith("Portraits: 0 downloaded, 0 taken from the cache, 3 failed", lines[0]);
        }

        [Fact]
        public void Cache_ListsTheFiles()
        {
            // Arrange
            _f.Run("remote portrait albedo");

            // Act
            var lines = _f.Run("remote cache");

            // Assert
            Assert.Equal($"=== PICTURE CACHE (1 file(s), {PngBytes.Pixel.Length} bytes) ===", lines[0]);
            Assert.Equal($"  Folder: {_f.Images.Folder}", lines[1]);
            Assert.StartsWith($"  albedo-portrait ({PngBytes.Pixel.Length} bytes,", lines[2]);
        }

        [Fact]
        public void CacheClear_DeletesThePictures()
        {
            // Arrange
            _f.Run("remote portraits");

            // Act
            var lines = _f.Run("remote cache clear");

            // Assert
            Assert.Equal("Cache cleared: 3 file(s) removed.", Assert.Single(lines));
            Assert.Empty(_f.Images.List());
        }

        [Fact]
        public void WithoutACache_TheCommandsSayTheyAreNotConfigured()
        {
            // Arrange
            var plain = new RemoteCliFixture();

            // Act
            var lines = plain.Run("remote portrait albedo");

            // Assert
            Assert.Equal("The picture cache is not configured.", Assert.Single(lines));
        }
    }
}