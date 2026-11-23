using System;
using System.Collections.Generic;
using System.Linq;
using GameSystem.Core.Abilities;
using GameSystem.Core.Logging;
using GameSystem.Core.Models;
using GameSystem.Core.Remote;
using Infra.Data;
using OOPproject.SOLID;
using ProjectTests.Mocks;
using Xunit;

namespace ProjectTests.CliTests
{
    /// <summary>Characters + remote handlers wired to a fake remote database and shared session.</summary>
    public class RemoteCliFixture
    {
        public DisplayerMock Displayer { get; } = new DisplayerMock();
        public InputReaderStub Input { get; } = new InputReaderStub();
        public InMemoryStorage Storage { get; } = new InMemoryStorage();
        public FakeGameDataApi Api { get; } = new FakeGameDataApi();
        public CommandInterpreter Interpreter { get; }

        public RemoteCliFixture()
        {
            Api.Characters["albedo"] = RemoteSamples.Albedo();
            Api.Characters["amber"] = RemoteSamples.Simple("amber", "Amber");
            Api.Characters["bennett"] = RemoteSamples.Simple("bennett", "Bennett");
            Api.Weapons["skyward-blade"] = RemoteSamples.SkywardBlade();
            Api.Weapons["dull-blade"] = RemoteSamples.DullBlade();

            var ui = new Presenter(Displayer, Input);
            var session = new GameSession();
            Interpreter = new CommandInterpreter(ui);

            new CharactersCliHandler(new DisplayerLogger(Displayer), ui, new SaveManager(Storage), session)
                .SetupCommands(Interpreter);

            new RemoteCliHandler(Api, new RemoteImporter(new StatGenerator(new Random(7))), session, ui, new CharacterSheetBuilder())
                .SetupCommands(Interpreter);
        }

        public IReadOnlyList<string> Run(string command, params string[] answers)
        {
            Displayer.Reset();
            Input.Clear();
            Input.Enqueue(answers);
            Interpreter.Execute(command);
            return Displayer.Lines.ToList();
        }
    }

    public class CharacterSheetBuilderTests
    {
        private static string[] Lines(string text) =>
            text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

        private static ApiCharacter NewAlbedo()
        {
            var profile = new CharacterProfile("albedo", "Kreideprinz", "Geo", "Sword", "Mondstadt",
                                               "Knights of Favonius", "Chief Alchemist.", 5);
            var burst = new CustomAbility("Burst", 3, 6, "Big hit.");
            return new ApiCharacter("Albedo", profile, new CharacterStats(120, 7, 21), burst);
        }

        private static string[] Sheet(Character character)
        {
            var view = new CharacterSheetBuilder().Build(character);
            return Lines(view.Render());
        }

        // ---------- Wrap / Shorten ----------

        [Fact]
        public void Wrap_BreaksLinesAtWidth()
        {
            // Act
            var lines = CharacterSheetBuilder.Wrap("aaa bbb ccc", 7);

            // Assert
            Assert.Equal(new[] { "aaa bbb", "ccc" }, lines);
        }

        [Fact]
        public void Wrap_LongWordStaysAlone()
        {
            // Act
            var lines = CharacterSheetBuilder.Wrap("tiny enormousword tiny", 6);

            // Assert
            Assert.Equal(new[] { "tiny", "enormousword", "tiny" }, lines);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Wrap_BlankText_GivesNoLines(string text)
        {
            // Act & Assert
            Assert.Empty(CharacterSheetBuilder.Wrap(text, 10));
        }

        [Fact]
        public void Shorten_CutsLongTextAndAddsDots()
        {
            // Act & Assert
            Assert.Equal("abcde...", CharacterSheetBuilder.Shorten("abcdefghij", 8));
        }

        [Fact]
        public void Shorten_ShortTextIsKeptAndWhitespaceCollapsed()
        {
            // Act & Assert
            Assert.Equal("a b", CharacterSheetBuilder.Shorten("  a \n  b ", 20));
        }

        // ---------- sheet of an imported character ----------

        [Fact]
        public void Build_ApiCharacter_HasTitleAndSections()
        {
            // Act
            var lines = Sheet(NewAlbedo());

            // Assert
            Assert.Equal("Title: Albedo", lines[0]);
            Assert.Contains("    Profile", lines);
            Assert.Contains("    Stats", lines);
            Assert.Contains("    Equipment", lines);
            Assert.Contains("    Abilities", lines);
            Assert.Contains("        Unique", lines);
            Assert.Contains("        Learned", lines);
        }

        [Fact]
        public void Build_ApiCharacter_ShowsProfile()
        {
            // Act
            var lines = Sheet(NewAlbedo());

            // Assert
            Assert.Contains("        Source: remote database (albedo)", lines);
            Assert.Contains("        Title: Kreideprinz", lines);
            Assert.Contains("        Vision: Geo | Weapon type: Sword | Rarity: 5", lines);
            Assert.Contains("        Nation: Mondstadt | Affiliation: Knights of Favonius", lines);
            Assert.Contains("        Chief Alchemist.", lines);
        }

        [Fact]
        public void Build_ShowsStats()
        {
            // Act
            var lines = Sheet(NewAlbedo());

            // Assert
            Assert.Contains("        Health: 120/120", lines);
            Assert.Contains("        Armor: 7", lines);
            Assert.Contains("        Attack: 21", lines);
            Assert.Contains("        Status: alive", lines);
        }

        [Fact]
        public void Build_WithoutItem_ShowsNone()
        {
            // Act
            var lines = Sheet(NewAlbedo());
            int equipment = Array.IndexOf(lines, "    Equipment");

            // Assert
            Assert.Equal("        none", lines[equipment + 1]);
        }

        [Fact]
        public void Build_WithItem_ShowsNameBonusesAndDescription()
        {
            // Arrange
            var character = NewAlbedo();
            character.Equip(new Item("Skyward Blade", 10, 2, 9, "Sword, rarity 5"), new DisplayerLogger(new DisplayerFake()));

            // Act
            var lines = Sheet(character);

            // Assert
            Assert.Contains("        Skyward Blade", lines);
            Assert.Contains("        Bonuses: +9 ATK, +2 ARM, +10 HP", lines);
            Assert.Contains("        Sword, rarity 5", lines);
        }

        [Fact]
        public void Build_ShowsUniqueAndLearnedAbilitiesWithDescriptions()
        {
            // Arrange
            var character = NewAlbedo();
            character.LearnAbility(new CustomAbility("Skill", 2, 3, "Zap."), new DisplayerLogger(new DisplayerFake()));

            // Act
            var lines = Sheet(character);

            // Assert
            Assert.Contains("            Burst - 3 attack(s) with +6 ATK while used", lines);
            Assert.Contains("            Big hit.", lines);
            Assert.Contains("            Skill - 2 attack(s) with +3 ATK while used", lines);
            Assert.Contains("            Zap.", lines);
        }

        [Fact]
        public void Build_NoLearnedAbilities_ShowsNone()
        {
            // Act
            var lines = Sheet(NewAlbedo());
            int learned = Array.IndexOf(lines, "        Learned");

            // Assert
            Assert.Equal("            none", lines[learned + 1]);
        }

        [Fact]
        public void Build_BuiltInCharacter_ShowsClass()
        {
            // Act
            var lines = Sheet(new HalfOrcBarbarian("Growler"));

            // Assert
            Assert.Equal("Title: Growler", lines[0]);
            Assert.Contains("        Class: HalfOrcBarbarian", lines);
            Assert.Contains("            Rage - Rage", lines);
        }

        [Fact]
        public void Build_LongDescription_IsWrappedIntoSeveralParagraphs()
        {
            // Arrange
            string longText = string.Join(" ", Enumerable.Repeat("word", 60));
            var profile = new CharacterProfile("x", "", "Pyro", "Bow", "", "", longText, 4);
            var character = new ApiCharacter("X", profile, new CharacterStats(100, 5, 15), new CustomAbility("A", 1, 0));

            // Act
            var lines = Sheet(character);

            // Assert
            Assert.Contains(lines, l => l.StartsWith("        word word"));
            Assert.All(lines, l => Assert.True(l.Length <= 8 + 76));
        }

        [Fact]
        public void Build_DefeatedCharacter_ShowsStatus()
        {
            // Arrange
            var character = NewAlbedo();
            character.RestoreState(0, 120, 7, 21, null, Array.Empty<GameSystem.Core.Abilities.IAbility>());

            // Act
            var lines = Sheet(character);

            // Assert
            Assert.Contains("        Status: defeated", lines);
        }
    }

    public class RemoteCliHandlerTests
    {
        private readonly RemoteCliFixture _f = new RemoteCliFixture();

        private static string[] Lines(string text) =>
            text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

        // ---------- usage ----------

        [Fact]
        public void Remote_WithoutArguments_ShowsUsage()
        {
            // Act
            var lines = _f.Run("remote");

            // Assert
            Assert.StartsWith("Usage: remote", Assert.Single(lines));
        }

        [Theory]
        [InlineData("remote ls")]
        [InlineData("remote ls spaceship")]
        [InlineData("remote fly char x")]
        public void Remote_InvalidArguments_ShowUsage(string command)
        {
            // Act
            var lines = _f.Run(command);

            // Assert
            Assert.StartsWith("Usage: remote", Assert.Single(lines));
        }

        [Theory]
        [InlineData("remote show char", "Usage: remote show <char|weapon> <id>")]
        [InlineData("remote import weapon", "Usage: remote import <char|weapon> <id>")]
        public void Remote_ShowOrImportWithoutId_ShowsUsage(string command, string expected)
        {
            // Act
            var lines = _f.Run(command);

            // Assert
            Assert.Equal(expected, Assert.Single(lines));
        }

        // ---------- remote ls ----------

        [Fact]
        public void RemoteLs_Characters_ListsAllIds()
        {
            // Act
            var lines = _f.Run("remote ls char");

            // Assert
            Assert.Equal("=== REMOTE CHARACTERS (3) ===", lines[0]);
            Assert.Contains("albedo", lines[1]);
            Assert.Contains("amber", lines[1]);
            Assert.Contains("bennett", lines[1]);
            Assert.Equal(new[] { "characters" }, _f.Api.Calls);
        }

        [Fact]
        public void RemoteLs_Weapons_ListsAllIds()
        {
            // Act
            var lines = _f.Run("remote ls weapon");

            // Assert
            Assert.Equal("=== REMOTE WEAPONS (2) ===", lines[0]);
            Assert.Contains("skyward-blade", lines[1]);
            Assert.Contains("dull-blade", lines[1]);
        }

        [Fact]
        public void RemoteLs_Find_FiltersIds()
        {
            // Act
            var lines = _f.Run("remote ls char --find alb");

            // Assert
            Assert.Equal("=== REMOTE CHARACTERS (1) ===", lines[0]);
            Assert.Equal("  albedo", lines[1]);
        }

        [Fact]
        public void RemoteLs_FindWithSpaces_MatchesDashedIds()
        {
            // Act
            var lines = _f.Run("remote ls weapon --find \"skyward blade\"");

            // Assert
            Assert.Equal("=== REMOTE WEAPONS (1) ===", lines[0]);
        }

        [Fact]
        public void RemoteLs_FindWithoutMatches_SaysNothingFound()
        {
            // Act
            var lines = _f.Run("remote ls char --find zzz");

            // Assert
            Assert.Equal("=== REMOTE CHARACTERS (0) ===", lines[0]);
            Assert.Equal("  (nothing found)", lines[1]);
        }

        [Fact]
        public void RemoteLs_Limit_CutsTheListAndSaysHowManyAreHidden()
        {
            // Act
            var lines = _f.Run("remote ls char --limit 1");

            // Assert
            Assert.Equal(3, lines.Count);
            Assert.Equal("=== REMOTE CHARACTERS (3) ===", lines[0]);
            Assert.Equal("  albedo", lines[1]);
            Assert.Equal("  ... and 2 more (use --find <text> or --limit <n>)", lines[2]);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("abc")]
        public void RemoteLs_InvalidLimit_IsRejected(string limit)
        {
            // Act
            var lines = _f.Run($"remote ls char --limit {limit}");

            // Assert
            Assert.Equal("--limit must be a positive integer.", Assert.Single(lines));
        }

        // ---------- remote show ----------

        [Fact]
        public void RemoteShow_Character_PrintsProfileAndTalents()
        {
            // Act
            var text = Assert.Single(_f.Run("remote show char albedo"));

            // Assert
            var lines = Lines(text);
            Assert.Equal("=== REMOTE CHARACTER: Albedo (albedo) ===", lines[0]);
            Assert.Contains("Kreideprinz", text);
            Assert.Contains("    - Rite of Progeniture: Tectonic Tide (Elemental Burst)", lines);
        }

        [Fact]
        public void RemoteShow_Weapon_PrintsDetails()
        {
            // Act
            var text = Assert.Single(_f.Run("remote show weapon skyward-blade"));

            // Assert
            Assert.Contains("=== REMOTE WEAPON: Skyward Blade (skyward-blade) ===", text);
            Assert.Contains("Base attack: 46", text);
            Assert.Contains("Sky-Piercing Fang", text);
        }

        [Fact]
        public void RemoteShow_Unknown_ReportsTheError()
        {
            // Act
            var lines = _f.Run("remote show char missing");

            // Assert
            Assert.Equal("[Error executing command]: Character 'missing' was not found in the remote database.", Assert.Single(lines));
        }

        // ---------- remote import ----------

        [Fact]
        public void RemoteImport_Character_RegistersItAndReportsDetails()
        {
            // Act
            var lines = _f.Run("remote import char albedo");

            // Assert
            Assert.Equal(4, lines.Count);
            Assert.Equal("Imported character 'Albedo' with id 1 (rarity 5).", lines[0]);
            Assert.StartsWith("  Stats: HP ", lines[1]);
            Assert.Equal("  Unique ability: [1] Rite of Progeniture: Tectonic Tide", lines[2]);
            Assert.Equal("  Learned abilities: [2] Favonius Bladework - Weiss, [3] Abiogenesis: Solar Isotoma", lines[3]);
        }

        [Fact]
        public void RemoteImport_Character_AppearsInLocalList()
        {
            // Arrange
            _f.Run("remote import char albedo");

            // Act
            var lines = _f.Run("ls char");

            // Assert
            Assert.StartsWith("  [1] Albedo (ApiCharacter) HP ", lines[1]);
        }

        [Fact]
        public void RemoteImport_SameCharacterTwice_IsRejected()
        {
            // Arrange
            _f.Run("remote import char albedo");

            // Act
            var lines = _f.Run("remote import char albedo");

            // Assert
            Assert.Equal("[Error executing command]: Character 'Albedo' is already in the game.", Assert.Single(lines));
        }

        [Fact]
        public void RemoteImport_Weapon_RegistersItemAndHintsHowToEquip()
        {
            // Act
            var lines = _f.Run("remote import weapon skyward-blade");

            // Assert
            Assert.Equal("Imported weapon 'Skyward Blade' as item with id 1 (+9 ATK, +2 ARM, +10 HP).", lines[0]);
            Assert.StartsWith("  Equip it with: add item --char_id", lines[1]);
        }

        [Fact]
        public void RemoteImport_UnknownWeapon_ReportsTheError()
        {
            // Act
            var lines = _f.Run("remote import weapon nothing");

            // Assert
            Assert.Equal("[Error executing command]: Weapon 'nothing' was not found in the remote database.", Assert.Single(lines));
        }

        // ---------- sheet ----------

        [Fact]
        public void Sheet_WithoutArguments_ShowsUsage()
        {
            // Act
            var lines = _f.Run("sheet");

            // Assert
            Assert.Equal("Usage: sheet <id/name>", Assert.Single(lines));
        }

        [Fact]
        public void Sheet_UnknownCharacter_ReportsIt()
        {
            // Act
            var lines = _f.Run("sheet nobody");

            // Assert
            Assert.Equal("Character: 'nobody' not found.", Assert.Single(lines));
        }

        [Fact]
        public void Sheet_WorksForLocallyCreatedCharacters()
        {
            // Arrange
            _f.Run("create char", "Growler", "barbarian", "");

            // Act
            var sheet = Assert.Single(_f.Run("sheet Growler"));

            // Assert
            Assert.StartsWith("Title: Growler", sheet);
            Assert.Contains("Class: HalfOrcBarbarian", sheet);
        }

        // ---------- the whole scenario ----------

        [Fact]
        public void FullScenario_ImportCharacterAndWeapon_Equip_ShowSheet()
        {
            // Arrange
            _f.Run("remote import char albedo");
            _f.Run("remote import weapon skyward-blade");

            // Act
            var equip = _f.Run("add item --char_id albedo --id \"Skyward Blade\"");
            var sheet = Lines(Assert.Single(_f.Run("sheet albedo")));

            // Assert
            Assert.Equal("Albedo equipped Skyward Blade (+9 ATK, +2 ARM, +10 HP).", Assert.Single(equip));
            Assert.Equal("Title: Albedo", sheet[0]);
            Assert.Contains("        Vision: Geo | Weapon type: Sword | Rarity: 5", sheet);
            Assert.Contains("        Skyward Blade", sheet);
            Assert.Contains("        Bonuses: +9 ATK, +2 ARM, +10 HP", sheet);
            Assert.Contains("            Rite of Progeniture: Tectonic Tide - 3 attack(s) with +6 ATK while", sheet);
            Assert.Contains("            used", sheet);
        }

        [Fact]
        public void ImportedCharacter_FightsLikeAnyOther()
        {
            // Arrange
            _f.Run("remote import char albedo");
            _f.Run("create char", "Growler", "barbarian", "");

            // Act
            var lines = _f.Run("act ability albedo Growler");

            // Assert
            Assert.Equal("Albedo uses Rite of Progeniture: Tectonic Tide!", lines[0]);
            Assert.Equal(4, lines.Count); // announcement + three hits
        }

        [Fact]
        public void ImportedCharacter_SurvivesSaveAndLoad()
        {
            // Arrange
            _f.Run("remote import char albedo");
            _f.Run("save remote");
            _f.Run("create char", "Growler", "barbarian", "");

            // Act
            var loaded = _f.Run("load remote", "y");
            var sheet = _f.Run("sheet albedo");

            // Assert
            Assert.Equal("Loaded 'remote': 1 character(s), 0 item(s), 3 ability(ies).", Assert.Single(loaded));
            Assert.StartsWith("Title: Albedo", Assert.Single(sheet));
        }
    }
}
