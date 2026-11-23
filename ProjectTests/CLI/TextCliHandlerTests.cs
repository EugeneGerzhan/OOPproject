using System;
using Xunit;

namespace ProjectTests.CliTests
{
    public class TextCliHandlerTests
    {
        private readonly TextCliFixture _f = new TextCliFixture();

        private static string Join(params string[] lines) => string.Join(Environment.NewLine, lines);

        // ---------- pwd ----------

        [Fact]
        public void Pwd_InRoot_ShowsSlash()
        {
            // Act
            var lines = _f.Run("pwd");

            // Assert
            Assert.Equal("Current Document Path: /", Assert.Single(lines));
        }

        [Fact]
        public void Pwd_InsideHeading_ShowsPath()
        {
            // Arrange
            _f.Factory.AddHeading("A");

            // Act
            var lines = _f.Run("pwd");

            // Assert
            Assert.Equal("Current Document Path: /A", Assert.Single(lines));
        }

        // ---------- add ----------

        [Fact]
        public void Add_ContainerHeading_WithInlineTitle()
        {
            // Act
            var lines = _f.Run("add container heading \"Chapter 1\"");

            // Assert
            Assert.Equal("Heading 'Chapter 1' added (id 1). Current path: /Chapter 1", Assert.Single(lines));
            Assert.Equal("/Chapter 1", _f.Factory.GetCurrentPath());
        }

        [Fact]
        public void Add_HeadingShortcut_WithoutCategory()
        {
            // Act
            var lines = _f.Run("add heading \"Fast\"");

            // Assert
            Assert.Equal("Heading 'Fast' added (id 1). Current path: /Fast", Assert.Single(lines));
        }

        [Fact]
        public void Add_LeafParagraph_GoesIntoCurrentHeading()
        {
            // Arrange
            _f.Run("add container heading \"Chapter 1\"");

            // Act
            var lines = _f.Run("add leaf paragraph \"Hello\"");

            // Assert
            Assert.Equal("Paragraph added (id 2).", Assert.Single(lines));
            Assert.Single(_f.Factory.Current.Children);
        }

        [Fact]
        public void Add_HeadingWithoutTitle_AsksInDialog()
        {
            // Act
            var lines = _f.Run("add container heading", "Dialog title");

            // Assert
            Assert.Equal("Heading 'Dialog title' added (id 1). Current path: /Dialog title", Assert.Single(lines));
            Assert.Equal("Enter heading title: ", Assert.Single(_f.Displayer.Writes));
        }

        [Fact]
        public void Add_ParagraphWithoutText_AsksInDialog()
        {
            // Act
            var lines = _f.Run("add leaf paragraph", "Some text");

            // Assert
            Assert.Equal("Paragraph added (id 1).", Assert.Single(lines));
            Assert.Equal("Enter paragraph content: ", Assert.Single(_f.Displayer.Writes));
        }

        [Fact]
        public void Add_EmptyDialogAnswer_ReportsError()
        {
            // Act
            var lines = _f.Run("add heading", "");

            // Assert
            Assert.Equal("[Error executing command]: Heading title cannot be empty.", Assert.Single(lines));
            Assert.Empty(_f.Factory.Document.Children);
        }

        [Fact]
        public void Add_NoArguments_ShowsUsage()
        {
            // Act
            var lines = _f.Run("add");

            // Assert
            Assert.StartsWith("Usage: add", Assert.Single(lines));
        }

        [Theory]
        [InlineData("add container paragraph x", "container paragraph")]
        [InlineData("add leaf heading x", "leaf heading")]
        public void Add_MismatchedCategoryAndType_IsRejected(string command, string shown)
        {
            // Act
            var lines = _f.Run(command);

            // Assert
            Assert.StartsWith($"Unknown element: '{shown}'", Assert.Single(lines));
            Assert.Empty(_f.Factory.Document.Children);
        }

        // ---------- print ----------

        [Fact]
        public void Print_EmptyDocument_ShowsTitleOnly()
        {
            // Act
            var lines = _f.Run("print");

            // Assert
            Assert.Equal("Title: Doc", Assert.Single(lines));
        }

        [Fact]
        public void Print_Whole_WithIds_ShowsEverything()
        {
            // Arrange
            _f.Factory.AddHeading("A");
            _f.Factory.AddParagraph("p");

            // Act
            var lines = _f.Run("print --whole --id");

            // Assert
            Assert.Equal(Join("[0] Title: Doc", "    [1] A", "        [2] p"), Assert.Single(lines));
        }

        [Fact]
        public void Print_Current_ShowsOnlyCurrentSubtree()
        {
            // Arrange
            _f.Factory.AddHeading("A");
            _f.Factory.AddParagraph("p");

            // Act
            var lines = _f.Run("print");

            // Assert
            Assert.Equal(Join("A", "    p"), Assert.Single(lines));
        }

        [Fact]
        public void Print_Current_WithIds()
        {
            // Arrange
            _f.Factory.AddHeading("A");
            _f.Factory.AddParagraph("p");

            // Act
            var lines = _f.Run("print --id");

            // Assert
            Assert.Equal(Join("[1] A", "    [2] p"), Assert.Single(lines));
        }

        // ---------- up ----------

        [Fact]
        public void Up_InRoot_SaysAlreadyInRoot()
        {
            // Act
            var lines = _f.Run("up");

            // Assert
            Assert.Equal("You are already in root.", Assert.Single(lines));
        }

        [Fact]
        public void Up_InsideHeading_MovesToParent()
        {
            // Arrange
            _f.Factory.AddHeading("A");

            // Act
            var lines = _f.Run("up");

            // Assert
            Assert.Equal("Moved up to parent element. Current path: /", Assert.Single(lines));
        }

        // ---------- cd ----------

        [Fact]
        public void Cd_ByPath_MovesThere()
        {
            // Arrange
            _f.Factory.AddHeading("A");
            _f.Factory.Up();

            // Act
            var lines = _f.Run("cd /A");

            // Assert
            Assert.Equal("Current path: /A", Assert.Single(lines));
        }

        [Fact]
        public void Cd_PathWithSpacesWithoutQuotes_IsJoined()
        {
            // Arrange
            _f.Factory.AddHeading("My Chapter");
            _f.Factory.Up();

            // Act
            var lines = _f.Run("cd /My Chapter");

            // Assert
            Assert.Equal("Current path: /My Chapter", Assert.Single(lines));
        }

        [Fact]
        public void Cd_MissingContainer_ReportsError()
        {
            // Act
            var lines = _f.Run("cd Missing");

            // Assert
            Assert.Equal("Container 'Missing' not found in '/'.", Assert.Single(lines));
        }

        [Fact]
        public void Cd_ById_MovesThere()
        {
            // Arrange
            _f.Factory.AddHeading("A");
            _f.Factory.Up();

            // Act
            var lines = _f.Run("cd --id 1");

            // Assert
            Assert.Equal("Current path: /A", Assert.Single(lines));
        }

        [Fact]
        public void Cd_InvalidId_ShowsUsage()
        {
            // Act
            var lines = _f.Run("cd --id abc");

            // Assert
            Assert.Equal("Usage: cd <path> | cd --id <container_id>", Assert.Single(lines));
        }

        [Fact]
        public void Cd_WithoutArguments_ShowsUsage()
        {
            // Act
            var lines = _f.Run("cd");

            // Assert
            Assert.Equal("Usage: cd <path> | cd --id <container_id>", Assert.Single(lines));
        }

        // ---------- rm ----------

        [Fact]
        public void Rm_ByName_Confirmed_RemovesElement()
        {
            // Arrange
            _f.Factory.AddHeading("A");
            _f.Factory.Up();

            // Act
            var lines = _f.Run("rm A", "y");

            // Assert
            Assert.Equal("Element removed. Current path: /", Assert.Single(lines));
            Assert.Equal("Are you sure you want to remove Heading 'A' (id 1)? (y/n): ", Assert.Single(_f.Displayer.Writes));
            Assert.Empty(_f.Factory.Document.Children);
        }

        [Fact]
        public void Rm_ByName_Declined_KeepsElement()
        {
            // Arrange
            _f.Factory.AddHeading("A");
            _f.Factory.Up();

            // Act
            var lines = _f.Run("rm A", "n");

            // Assert
            Assert.Equal("Removal cancelled.", Assert.Single(lines));
            Assert.Single(_f.Factory.Document.Children);
        }

        [Fact]
        public void Rm_WithoutName_RemovesCurrentAndGoesToParent()
        {
            // Arrange
            _f.Factory.AddHeading("A");

            // Act
            var lines = _f.Run("rm", "y");

            // Assert
            Assert.Equal("Element removed. Current path: /", Assert.Single(lines));
            Assert.Empty(_f.Factory.Document.Children);
        }

        [Fact]
        public void Rm_Root_IsRejectedWithoutAskingConfirmation()
        {
            // Act
            var lines = _f.Run("rm", "y");

            // Assert
            Assert.Equal("Root cannot be removed.", Assert.Single(lines));
            Assert.Empty(_f.Displayer.Writes);
        }

        [Fact]
        public void Rm_MissingName_ReportsError()
        {
            // Act
            var lines = _f.Run("rm Missing");

            // Assert
            Assert.Equal("Element 'Missing' not found in the current element.", Assert.Single(lines));
        }

        [Fact]
        public void Rm_ById_RemovesElement()
        {
            // Arrange
            _f.Factory.AddHeading("A");
            _f.Factory.Up();

            // Act
            var lines = _f.Run("rm --id 1", "y");

            // Assert
            Assert.Equal("Element removed. Current path: /", Assert.Single(lines));
            Assert.Empty(_f.Factory.Document.Children);
        }

        [Fact]
        public void Rm_UnknownId_ReportsError()
        {
            // Act
            var lines = _f.Run("rm --id 99");

            // Assert
            Assert.Equal("Element with id 99 not found.", Assert.Single(lines));
        }

        [Fact]
        public void Rm_InvalidId_ShowsUsage()
        {
            // Act
            var lines = _f.Run("rm --id abc");

            // Assert
            Assert.Equal("Usage: rm [<elem_name>] | rm --id <element_id>", Assert.Single(lines));
        }

        // ---------- save / load / saves ----------

        [Fact]
        public void Save_StoresDocumentUnderName()
        {
            // Arrange
            _f.Factory.AddHeading("Saved");

            // Act
            var lines = _f.Run("save demo");

            // Assert
            Assert.Equal("Document saved as 'demo' (memory).", Assert.Single(lines));
            Assert.True(_f.Storage.Load().TextSaves.ContainsKey("demo"));
        }

        [Fact]
        public void Save_NameWithSpaces_IsJoined()
        {
            // Act
            var lines = _f.Run("save My Doc");

            // Assert
            Assert.Equal("Document saved as 'My Doc' (memory).", Assert.Single(lines));
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
        public void Load_Confirmed_RestoresSavedDocument()
        {
            // Arrange
            _f.Factory.AddHeading("Saved");
            _f.Factory.Up();
            _f.Run("save demo");
            _f.Factory.AddHeading("Extra");
            _f.Factory.Up();

            // Act
            var lines = _f.Run("load demo", "y");

            // Assert
            Assert.Equal("Document 'demo' loaded. Current path: /", Assert.Single(lines));
            Assert.Equal(Join("Title: Doc", "    Saved"), _f.Factory.GetWholeView().Render());
        }

        [Fact]
        public void Load_Declined_KeepsCurrentDocument()
        {
            // Arrange
            _f.Run("save demo");
            _f.Factory.AddHeading("Extra");
            _f.Factory.Up();

            // Act
            var lines = _f.Run("load demo", "n");

            // Assert
            Assert.Equal("Loading cancelled.", Assert.Single(lines));
            Assert.Single(_f.Factory.Document.Children);
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
        public void Load_WithoutName_ShowsUsage()
        {
            // Act
            var lines = _f.Run("load");

            // Assert
            Assert.Equal("Usage: load <name>", Assert.Single(lines));
        }

        [Fact]
        public void Saves_WhenEmpty_SaysSo()
        {
            // Act
            var lines = _f.Run("saves");

            // Assert
            Assert.Equal("No saved documents (memory).", Assert.Single(lines));
        }

        [Fact]
        public void Saves_ListsSavesSortedByName()
        {
            // Arrange
            _f.Run("save b");
            _f.Run("save a");

            // Act
            var lines = _f.Run("saves");

            // Assert
            Assert.Equal(3, lines.Count);
            Assert.Equal("=== SAVED DOCUMENTS ===", lines[0]);
            Assert.StartsWith("  a (", lines[1]);
            Assert.StartsWith("  b (", lines[2]);
        }

        // ---------- help ----------

        [Fact]
        public void Help_ListsCommands()
        {
            // Act
            var lines = _f.Run("help");

            // Assert
            Assert.Contains(lines, l => l.TrimStart().StartsWith("pwd"));
            Assert.Contains(lines, l => l.TrimStart().StartsWith("save <name>"));
            Assert.Contains(lines, l => l.TrimStart().StartsWith("cd <path>"));
        }
    }
}