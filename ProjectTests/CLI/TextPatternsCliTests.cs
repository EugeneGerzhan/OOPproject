using System;
using Xunit;

namespace ProjectTests.CliTests
{
    public class TextPatternsCliTests
    {
        private readonly TextCliFixture _f = new TextCliFixture();

        private static string Join(params string[] lines) => string.Join(Environment.NewLine, lines);

        // ---------- print --format ----------

        [Fact]
        public void Print_WholeAsHtml()
        {
            // Arrange
            _f.Factory.AddHeading("A");
            _f.Factory.AddParagraph("p");

            // Act
            var lines = _f.Run("print --whole --format html");

            // Assert
            Assert.Equal(
                Join("<article>", "  <h1>Doc</h1>", "  <section>", "    <h2>A</h2>", "    <p>p</p>", "  </section>", "</article>"),
                Assert.Single(lines));
        }

        [Fact]
        public void Print_CurrentAsHtml_StartsFromTheCurrentElement()
        {
            // Arrange
            _f.Factory.AddHeading("A");
            _f.Factory.AddParagraph("p");

            // Act
            var lines = _f.Run("print --format html");

            // Assert
            Assert.Equal(Join("<section>", "  <h1>A</h1>", "  <p>p</p>", "</section>"), Assert.Single(lines));
        }

        [Fact]
        public void Print_WholeAsMarkdown()
        {
            // Arrange
            _f.Factory.AddHeading("A");
            _f.Factory.AddParagraph("p");

            // Act
            var lines = _f.Run("print --whole --format markdown");

            // Assert
            Assert.Equal(Join("# Doc", "", "## A", "", "p"), Assert.Single(lines));
        }

        [Fact]
        public void Print_FormatNameIsCaseInsensitive()
        {
            // Arrange
            _f.Factory.AddParagraph("p");

            // Act
            var lines = _f.Run("print --whole --format HTML");

            // Assert
            Assert.StartsWith("<article>", Assert.Single(lines));
        }

        [Fact]
        public void Print_ExplicitPlain_EqualsTheDefault()
        {
            // Arrange
            _f.Factory.AddHeading("A");

            // Act
            var explicitPlain = _f.Run("print --whole --format plain");
            var byDefault = _f.Run("print --whole");

            // Assert
            Assert.Equal(byDefault, explicitPlain);
        }

        [Fact]
        public void Print_UnknownFormat_IsReported()
        {
            // Act
            var lines = _f.Run("print --format pdf");

            // Assert
            Assert.Equal("Unknown format 'pdf'. Available: plain, html, markdown.", Assert.Single(lines));
        }

        // ---------- cp ----------

        [Fact]
        public void Cp_Heading_CopiesItWithAllContent()
        {
            // Arrange
            _f.Factory.AddHeading("Chapter");
            _f.Factory.AddParagraph("text");
            _f.Factory.Up();

            // Act
            var lines = _f.Run("cp Chapter");

            // Assert
            Assert.Equal("Copied Heading 'Chapter' (id 1) as 'Chapter (copy)' (id 3, 2 element(s)).", Assert.Single(lines));
            Assert.Equal(2, _f.Factory.Document.Children.Count);
        }

        [Fact]
        public void Cp_WithAsOption_UsesTheNewName()
        {
            // Arrange
            _f.Factory.AddHeading("Chapter");
            _f.Factory.AddParagraph("text");
            _f.Factory.Up();

            // Act
            var lines = _f.Run("cp Chapter --as \"Chapter 2\"");

            // Assert
            Assert.Equal("Copied Heading 'Chapter' (id 1) as 'Chapter 2' (id 3, 2 element(s)).", Assert.Single(lines));
        }

        [Fact]
        public void Cp_ById_CopiesTheElement()
        {
            // Arrange
            _f.Factory.AddHeading("Chapter");
            _f.Factory.Up();

            // Act
            var lines = _f.Run("cp --id 1 --as Second");

            // Assert
            Assert.Equal("Copied Heading 'Chapter' (id 1) as 'Second' (id 2, 1 element(s)).", Assert.Single(lines));
        }

        [Fact]
        public void Cp_Paragraph_CopiesTheText()
        {
            // Arrange
            _f.Factory.AddParagraph("hello");

            // Act
            var lines = _f.Run("cp hello");

            // Assert
            Assert.Equal("Copied Paragraph 'hello' (id 1) as 'hello' (id 2, 1 element(s)).", Assert.Single(lines));
        }

        [Fact]
        public void Cp_GivesTheCopyUniqueIds()
        {
            // Arrange
            _f.Factory.AddHeading("Chapter");
            _f.Factory.AddParagraph("text");
            _f.Factory.Up();
            _f.Run("cp Chapter");

            // Act
            var lines = _f.Run("print --whole --id");

            // Assert
            Assert.Equal(
                Join("[0] Title: Doc", "    [1] Chapter", "        [2] text", "    [3] Chapter (copy)", "        [4] text"),
                Assert.Single(lines));
        }

        [Fact]
        public void Cp_InsideAHeading_CopiesIntoIt()
        {
            // Arrange
            _f.Factory.AddHeading("Chapter");
            _f.Factory.AddParagraph("text");

            // Act
            _f.Run("cp text");

            // Assert
            Assert.Equal(2, _f.Factory.Current.Children.Count);
        }

        [Fact]
        public void Cp_MissingName_ReportsIt()
        {
            // Act
            var lines = _f.Run("cp Missing");

            // Assert
            Assert.Equal("Element 'Missing' not found in the current element.", Assert.Single(lines));
        }

        [Fact]
        public void Cp_UnknownId_ReportsIt()
        {
            // Act
            var lines = _f.Run("cp --id 99");

            // Assert
            Assert.Equal("Element with id 99 not found.", Assert.Single(lines));
        }

        [Theory]
        [InlineData("cp")]
        [InlineData("cp --id abc")]
        public void Cp_InvalidArguments_ShowUsage(string command)
        {
            // Act
            var lines = _f.Run(command);

            // Assert
            Assert.StartsWith("Usage: cp", Assert.Single(lines));
        }

        [Fact]
        public void Cp_Root_IsRejected()
        {
            // Act
            var lines = _f.Run("cp --id 0");

            // Assert
            Assert.Equal("[Error executing command]: Root 'Doc' cannot be copied.", Assert.Single(lines));
        }

        [Fact]
        public void Cp_InvalidNewName_IsRejected()
        {
            // Arrange
            _f.Factory.AddHeading("Chapter");
            _f.Factory.Up();

            // Act
            var lines = _f.Run("cp Chapter --as \"a/b\"");

            // Assert
            Assert.Equal("[Error executing command]: Heading title cannot contain '/'.", Assert.Single(lines));
            Assert.Single(_f.Factory.Document.Children);
        }
    }

    public class SheetFormatCliTests
    {
        private readonly RemoteCliFixture _f = new RemoteCliFixture();

        [Fact]
        public void Sheet_AsHtml_RendersTheSheetAsHtml()
        {
            // Arrange
            _f.Run("remote import char albedo");

            // Act
            var html = Assert.Single(_f.Run("sheet albedo --format html"));

            // Assert
            Assert.StartsWith("<article>", html);
            Assert.Contains("<h1>Albedo</h1>", html);
            Assert.Contains("<h2>Profile</h2>", html);
            Assert.Contains("<h3>Unique</h3>", html);
        }

        [Fact]
        public void Sheet_AsMarkdown_RendersTheSheetAsMarkdown()
        {
            // Arrange
            _f.Run("remote import char albedo");

            // Act
            var markdown = Assert.Single(_f.Run("sheet albedo --format markdown"));

            // Assert
            Assert.StartsWith("# Albedo", markdown);
            Assert.Contains("## Profile", markdown);
        }

        [Fact]
        public void Sheet_UnknownFormat_IsReported()
        {
            // Act
            var lines = _f.Run("sheet albedo --format pdf");

            // Assert
            Assert.Equal("Unknown format 'pdf'. Available: plain, html, markdown.", Assert.Single(lines));
        }
    }
}
