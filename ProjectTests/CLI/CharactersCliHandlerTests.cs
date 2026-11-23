using System.Linq;
using Infra.Data;
using Xunit;

namespace ProjectTests.CliTests
{
    public class CharactersCliHandlerTests
    {
        private readonly CharactersCliFixture _f = new CharactersCliFixture();

        // ---------- create ----------

        [Fact]
        public void Create_NoArguments_ShowsUsage()
        {
            // Act
            var lines = _f.Run("create");

            // Assert
            Assert.Equal("Usage: create <char|item|ability>", Assert.Single(lines));
        }

        [Fact]
        public void Create_UnknownKind_ShowsUsage()
        {
            // Act
            var lines = _f.Run("create spaceship");

            // Assert
            Assert.Equal("Usage: create <char|item|ability>", Assert.Single(lines));
        }

        [Fact]
        public void CreateChar_Barbarian_RegistersCharacterAndItsAbility()
        {
            // Act
            var lines = _f.Run("create char", "Growler", "barbarian", "");

            // Assert
            Assert.Equal(new[]
            {
                "Character 'Growler' (HalfOrcBarbarian) created with id 1.",
                "Its unique ability 'Rage' is registered with id 1."
            }, lines);
        }

        [Fact]
        public void CreateChar_EmptyClassAnswer_UsesFirstClass()
        {
            // Act
            var lines = _f.Run("create char", "Growler", "", "");

            // Assert
            Assert.Equal("Character 'Growler' (HalfOrcBarbarian) created with id 1.", lines[0]);
        }

        [Fact]
        public void CreateChar_Monk()
        {
            // Act
            var lines = _f.Run("create char", "Shi Fuu", "monk", "");

            // Assert
            Assert.Equal("Character 'Shi Fuu' (Monk) created with id 1.", lines[0]);
            Assert.Equal("Its unique ability 'Flurry of Blows' is registered with id 1.", lines[1]);
        }

        [Fact]
        public void CreateChar_EmptyName_IsRejected()
        {
            // Act
            var lines = _f.Run("create char", "");

            // Assert
            Assert.Equal("Name cannot be empty.", Assert.Single(lines));
        }

        [Fact]
        public void CreateChar_DuplicateName_IsRejected()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");

            // Act
            var lines = _f.Run("create char", "growler");

            // Assert
            Assert.Equal("Character 'growler' already exists.", Assert.Single(lines));
        }

        [Fact]
        public void CreateChar_InvalidClass_IsAskedAgain()
        {
            // Act
            var lines = _f.Run("create char", "Gimli", "wizard", "monk", "");

            // Assert
            Assert.Contains("Choose one of: barbarian, monk.", lines);
            Assert.Contains("Character 'Gimli' (Monk) created with id 1.", lines);
        }

        [Fact]
        public void CreateChar_WithRequestedId_UsesIt()
        {
            // Act
            var lines = _f.Run("create char", "A", "", "7");

            // Assert
            Assert.Equal("Character 'A' (HalfOrcBarbarian) created with id 7.", lines[0]);
        }

        [Fact]
        public void CreateChar_TakenId_IsAskedAgain()
        {
            // Arrange
            _f.Run("create char", "A", "", "7");

            // Act
            var lines = _f.Run("create char", "B", "", "7", "8");

            // Assert
            Assert.Contains("Id 7 is already taken.", lines);
            Assert.Contains("Character 'B' (HalfOrcBarbarian) created with id 8.", lines);
        }

        [Fact]
        public void CreateChar_InvalidId_IsAskedAgain()
        {
            // Act
            var lines = _f.Run("create char", "A", "", "abc", "");

            // Assert
            Assert.Contains("Id must be a positive number.", lines);
            Assert.Contains("Character 'A' (HalfOrcBarbarian) created with id 1.", lines);
        }

        [Fact]
        public void CreateItem_StoresBonusesFromDialog()
        {
            // Act
            var lines = _f.Run("create item", "Axe", "0", "0", "5", "");

            // Assert
            Assert.Equal("Item 'Axe' created with id 1.", Assert.Single(lines));
            var ls = _f.Run("ls item");
            Assert.Equal("  [1] Axe (+5 ATK, +0 ARM, +0 HP) | used by: nobody", ls[1]);
        }

        [Fact]
        public void CreateItem_EmptyName_IsRejected()
        {
            // Act
            var lines = _f.Run("create item", "");

            // Assert
            Assert.Equal("Name cannot be empty.", Assert.Single(lines));
        }

        [Fact]
        public void CreateAbility_Custom_UsesDialogValues()
        {
            // Act
            var lines = _f.Run("create ability", "custom", "Whirlwind", "3", "2", "");

            // Assert
            Assert.Equal("Ability 'Whirlwind' created with id 1.", Assert.Single(lines));
        }

        [Fact]
        public void CreateAbility_Rage_IsCreatedWithoutExtraQuestions()
        {
            // Act
            var lines = _f.Run("create ability", "rage", "");

            // Assert
            Assert.Equal("Ability 'Rage' created with id 1.", Assert.Single(lines));
        }

        // ---------- add ----------

        [Fact]
        public void Add_NoArguments_ShowsUsage()
        {
            // Act
            var lines = _f.Run("add");

            // Assert
            Assert.StartsWith("Usage: add", Assert.Single(lines));
        }

        [Fact]
        public void Add_UnknownKind_ShowsUsage()
        {
            // Act
            var lines = _f.Run("add weapon");

            // Assert
            Assert.StartsWith("Usage: add", Assert.Single(lines));
        }

        [Fact]
        public void Add_Item_ByNames_EquipsIt()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");
            _f.CreateItem("Axe", 0, 0, 5);

            // Act
            var lines = _f.Run("add item --char_id Growler --id Axe");

            // Assert
            Assert.Equal("Growler equipped Axe (+5 ATK, +0 ARM, +0 HP).", Assert.Single(lines));
        }

        [Fact]
        public void Add_Item_ByIds_EquipsIt()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");
            _f.CreateItem("Axe", 0, 0, 5);

            // Act
            var lines = _f.Run("add item --char_id 1 --id 1");

            // Assert
            Assert.Equal("Growler equipped Axe (+5 ATK, +0 ARM, +0 HP).", Assert.Single(lines));
        }

        [Fact]
        public void Add_Item_WithoutOptions_AsksInDialog()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");
            _f.CreateItem("Axe", 0, 0, 5);

            // Act
            var lines = _f.Run("add item", "Growler", "Axe");

            // Assert
            Assert.Equal(new[] { "Enter character id/name: ", "Enter item id/name: " }, _f.Displayer.Writes);
            Assert.Equal("Growler equipped Axe (+5 ATK, +0 ARM, +0 HP).", Assert.Single(lines));
        }

        [Fact]
        public void Add_Item_UnknownCharacter_ReportsIt()
        {
            // Arrange
            _f.CreateItem("Axe", 0, 0, 5);

            // Act
            var lines = _f.Run("add item --char_id Nobody --id Axe");

            // Assert
            Assert.Equal("Character: 'Nobody' not found.", Assert.Single(lines));
        }

        [Fact]
        public void Add_Item_UnknownItem_ReportsIt()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");

            // Act
            var lines = _f.Run("add item --char_id Growler --id Nothing");

            // Assert
            Assert.Equal("Item: 'Nothing' not found.", Assert.Single(lines));
        }

        [Fact]
        public void Add_Ability_TeachesIt()
        {
            // Arrange
            _f.CreateCharacter("Shi Fuu", "monk");
            _f.CreateCustomAbility("Whirlwind", 3, 2);

            // Act
            var lines = _f.Run("add ability --char_id \"Shi Fuu\" --id Whirlwind");

            // Assert
            Assert.Equal("Shi Fuu learned Whirlwind.", Assert.Single(lines));
        }

        // ---------- act ----------

        [Fact]
        public void Act_TooFewArguments_ShowsUsage()
        {
            // Act
            var lines = _f.Run("act attack Growler");

            // Assert
            Assert.StartsWith("Usage: act", Assert.Single(lines));
        }

        [Fact]
        public void Act_UnknownAction_IsRejected()
        {
            // Act
            var lines = _f.Run("act dance Growler \"Shi Fuu\"");

            // Assert
            Assert.StartsWith("Unknown action: dance.", Assert.Single(lines));
        }

        [Fact]
        public void Act_UnknownTarget_IsReported()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");

            // Act
            var lines = _f.Run("act attack Growler Missing");

            // Assert
            Assert.Equal("Target: 'Missing' not found.", Assert.Single(lines));
        }

        [Fact]
        public void Act_Attack_DamagesTarget()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");
            _f.CreateCharacter("Shi Fuu", "monk");

            // Act
            var lines = _f.Run("act attack Growler \"Shi Fuu\"");

            // Assert
            Assert.Equal("Growler attacks Shi Fuu for 12 damage.", Assert.Single(lines));
        }

        [Fact]
        public void Act_AttackSelf_IsRejected()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");

            // Act
            var lines = _f.Run("act attack Growler Growler");

            // Assert
            Assert.Equal("Growler cannot target itself with this action.", Assert.Single(lines));
        }

        [Fact]
        public void Act_Heal_HealsTheTargetNotTheActor()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");
            _f.CreateCharacter("Shi Fuu", "monk");
            _f.Run("act attack Growler \"Shi Fuu\""); // monk 78/90

            // Act
            var lines = _f.Run("act heal Growler \"Shi Fuu\"");

            // Assert
            Assert.Equal("Shi Fuu healed for 15 HP. Current Health: 90/90.", Assert.Single(lines));
        }

        [Fact]
        public void Act_Heal_WithAmountOption()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");
            _f.CreateCharacter("Shi Fuu", "monk");
            _f.Run("act attack Growler \"Shi Fuu\""); // monk 78/90

            // Act
            var lines = _f.Run("act heal Growler \"Shi Fuu\" --amount 5");

            // Assert
            Assert.Equal("Shi Fuu healed for 5 HP. Current Health: 83/90.", Assert.Single(lines));
        }

        [Theory]
        [InlineData("0")]
        [InlineData("abc")]
        public void Act_Heal_InvalidAmount_IsRejected(string amount)
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");
            _f.CreateCharacter("Shi Fuu", "monk");

            // Act
            var lines = _f.Run($"act heal Growler \"Shi Fuu\" --amount {amount}");

            // Assert
            Assert.Equal("--amount must be a positive integer.", Assert.Single(lines));
        }

        [Fact]
        public void Act_Ability_WithoutId_UsesUniqueAbility()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");
            _f.CreateCharacter("Shi Fuu", "monk");

            // Act
            var lines = _f.Run("act ability Growler \"Shi Fuu\"");

            // Assert
            Assert.Equal(new[]
            {
                "Growler enters a Rage! Attack increased, armor temporarily lowered.",
                "Growler attacks Shi Fuu for 22 damage."
            }, lines);
        }

        [Fact]
        public void Act_Ability_Flurry_HitsTwice()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");
            _f.CreateCharacter("Shi Fuu", "monk");

            // Act
            var lines = _f.Run("act ability \"Shi Fuu\" Growler");

            // Assert
            Assert.Equal(new[]
            {
                "Shi Fuu executes Flurry of Blows!",
                "Shi Fuu attacks Growler for 10 damage.",
                "Shi Fuu attacks Growler for 10 damage."
            }, lines);
        }

        [Fact]
        public void Act_Ability_ById_NotKnownByActor_IsRejected()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");
            _f.CreateCharacter("Shi Fuu", "monk");
            _f.CreateCustomAbility("Whirlwind", 3, 2);

            // Act
            var lines = _f.Run("act ability Growler \"Shi Fuu\" --id Whirlwind");

            // Assert
            Assert.Equal("Growler does not know 'Whirlwind'. Teach it first with 'add ability'.", Assert.Single(lines));
        }

        [Fact]
        public void Act_Ability_ById_AfterLearning_Executes()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");
            _f.CreateCharacter("Shi Fuu", "monk");
            _f.CreateCustomAbility("Whirlwind", 3, 2);
            _f.Run("add ability --char_id Growler --id Whirlwind");

            // Act
            var lines = _f.Run("act ability Growler \"Shi Fuu\" --id Whirlwind");

            // Assert
            Assert.Equal(4, lines.Count);
            Assert.Equal("Growler uses Whirlwind!", lines[0]);
            Assert.All(lines.Skip(1), l => Assert.Equal("Growler attacks Shi Fuu for 14 damage.", l));
        }

        [Fact]
        public void Act_DefeatedCharacters_CannotActOrBeHealed()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");
            _f.CreateCharacter("Shi Fuu", "monk");
            _f.CreateItem("Doom", 0, 0, 1000);
            _f.Run("add item --char_id Growler --id Doom");

            // Act
            var kill = _f.Run("act attack Growler \"Shi Fuu\"");
            var deadActs = _f.Run("act attack \"Shi Fuu\" Growler");
            var deadAttacked = _f.Run("act attack Growler \"Shi Fuu\"");
            var deadHealed = _f.Run("act heal Growler \"Shi Fuu\"");

            // Assert
            Assert.Equal(new[] { "Growler attacks Shi Fuu for 1012 damage.", "Shi Fuu has been defeated!" }, kill);
            Assert.Equal("Shi Fuu is defeated and cannot act.", Assert.Single(deadActs));
            Assert.Equal("Shi Fuu is already defeated.", Assert.Single(deadAttacked));
            Assert.Equal("Shi Fuu is defeated and cannot be healed.", Assert.Single(deadHealed));
        }

        // ---------- ls ----------

        [Fact]
        public void Ls_Empty_ShowsAllCategoriesAsEmpty()
        {
            // Act
            var lines = _f.Run("ls");

            // Assert
            Assert.Equal(new[]
            {
                "=== CHARACTERS ===", "  (none)",
                "=== ITEMS ===", "  (none)",
                "=== ABILITIES ===", "  (none)"
            }, lines);
        }

        [Fact]
        public void Ls_Char_ShowsShortState()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");

            // Act
            var lines = _f.Run("ls char");

            // Assert
            Assert.Equal("=== CHARACTERS ===", lines[0]);
            Assert.Equal("  [1] Growler (HalfOrcBarbarian) HP 120/120, ARM 5, ATK 20, alive", lines[1]);
        }

        [Fact]
        public void Ls_Ability_ShowsWhoCanUseIt()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");

            // Act
            var lines = _f.Run("ls ability");

            // Assert
            Assert.Equal("  [1] Rage | can be used by: Growler", lines[1]);
        }

        [Fact]
        public void Ls_Item_ShowsWhoUsesIt()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");
            _f.CreateItem("Axe", 0, 0, 5);
            _f.Run("add item --char_id Growler --id Axe");

            // Act
            var lines = _f.Run("ls item");

            // Assert
            Assert.Equal("  [1] Axe (+5 ATK, +0 ARM, +0 HP) | used by: Growler", lines[1]);
        }

        [Fact]
        public void Ls_CharWithId_ShowsFullState()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");

            // Act
            var lines = _f.Run("ls char --id Growler");

            // Assert
            Assert.StartsWith("=== CHARACTER [1] Growler ===", Assert.Single(lines));
        }

        [Fact]
        public void Ls_ItemWithId_ShowsFullState()
        {
            // Arrange
            _f.CreateItem("Axe", 0, 0, 5);

            // Act
            var lines = _f.Run("ls item --id Axe");

            // Assert
            Assert.StartsWith("=== ITEM [1] Axe ===", Assert.Single(lines));
        }

        [Fact]
        public void Ls_AbilityWithId_ShowsFullState()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");

            // Act
            var lines = _f.Run("ls ability --id Rage");

            // Assert
            Assert.StartsWith("=== ABILITY [1] Rage ===", Assert.Single(lines));
        }

        [Fact]
        public void Ls_UnknownId_ReportsIt()
        {
            // Act
            var lines = _f.Run("ls char --id Missing");

            // Assert
            Assert.Equal("Character: 'Missing' not found.", Assert.Single(lines));
        }

        [Fact]
        public void Ls_IdWithoutCategory_AsksForCategory()
        {
            // Act
            var lines = _f.Run("ls --id 1");

            // Assert
            Assert.Equal("Specify a category: ls <char|item|ability> --id <id/name>", Assert.Single(lines));
        }

        [Fact]
        public void Ls_UnknownCategory_ShowsUsage()
        {
            // Act
            var lines = _f.Run("ls spaceship");

            // Assert
            Assert.Equal("Usage: ls [<char|item|ability>] [--id <id/name>]", Assert.Single(lines));
        }

        // ---------- save / load ----------

        [Fact]
        public void Save_StoresGameState()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");

            // Act
            var lines = _f.Run("save arena");

            // Assert
            Assert.Equal("Game state saved as 'arena' (memory).", Assert.Single(lines));
            Assert.True(_f.Storage.Load().CharacterSaves.ContainsKey("arena"));
        }

        [Fact]
        public void Save_WithoutName_ShowsUsage()
        {
            // Act
            var lines = _f.Run("save");

            // Assert
            Assert.Equal("Usage: save <name>", Assert.Single(lines));
        }

        [Fact]
        public void Load_Confirmed_RestoresEarlierState()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");
            _f.CreateCharacter("Shi Fuu", "monk");
            _f.Run("act attack Growler \"Shi Fuu\"");   // monk 78/90
            _f.Run("save arena");
            _f.Run("act attack Growler \"Shi Fuu\"");   // monk 66/90

            // Act
            var lines = _f.Run("load arena", "y");
            var monk = _f.Run("ls char --id \"Shi Fuu\"");

            // Assert
            Assert.Equal("Loaded 'arena': 2 character(s), 0 item(s), 2 ability(ies).", Assert.Single(lines));
            Assert.Contains("78/90", Assert.Single(monk));
        }

        [Fact]
        public void Load_Declined_KeepsCurrentState()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");
            _f.Run("save arena");
            _f.CreateCharacter("Shi Fuu", "monk");

            // Act
            var lines = _f.Run("load arena", "n");
            var all = _f.Run("ls char");

            // Assert
            Assert.Equal("Loading cancelled.", Assert.Single(lines));
            Assert.Equal(3, all.Count); // header + 2 characters
        }

        [Fact]
        public void Load_UnknownName_ReportsIt()
        {
            // Act
            var lines = _f.Run("load nope");

            // Assert
            Assert.Equal("Save 'nope' not found. Use 'saves' to list available saves.", Assert.Single(lines));
        }

        [Fact]
        public void Load_CorruptedSave_IsReportedAndStateIsKept()
        {
            // Arrange
            _f.CreateCharacter("Growler", "barbarian");
            var bad = new CharacterSave { SaveName = "bad" };
            bad.Abilities.Add(new AbilityData(1, "laser", "Laser", 0, 0));
            _f.Storage.Load().CharacterSaves["bad"] = bad;

            // Act
            var lines = _f.Run("load bad", "y");
            var all = _f.Run("ls char");

            // Assert
            Assert.Equal("[Error executing command]: Unknown ability type 'laser'.", Assert.Single(lines));
            Assert.Contains("Growler", all[1]);
        }

        [Fact]
        public void Saves_WhenEmpty_SaysSo()
        {
            // Act
            var lines = _f.Run("saves");

            // Assert
            Assert.Equal("No saved game states (memory).", Assert.Single(lines));
        }

        [Fact]
        public void Saves_ListsSavedStates()
        {
            // Arrange
            _f.Run("save arena");

            // Act
            var lines = _f.Run("saves");

            // Assert
            Assert.Equal("=== SAVED GAME STATES ===", lines[0]);
            Assert.StartsWith("  arena (", lines[1]);
        }

        // ---------- help ----------

        [Fact]
        public void Help_ListsCommands()
        {
            // Act
            var lines = _f.Run("help");

            // Assert
            Assert.Contains(lines, l => l.TrimStart().StartsWith("create"));
            Assert.Contains(lines, l => l.TrimStart().StartsWith("act"));
            Assert.Contains(lines, l => l.TrimStart().StartsWith("save <name>"));
        }
    }
}