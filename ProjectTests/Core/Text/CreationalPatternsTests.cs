using System;
using System.Collections.Generic;
using System.Linq;
using OOPTextProject.TextSystem;
using OOPTextProject.TextSystem.Building;
using OOPTextProject.TextSystem.Formatting;
using ProjectTests.TextTests;
using Xunit;

namespace ProjectTests.PatternTests
{
    public class DocumentBuilderTests
    {
        private static string Join(params string[] lines) => string.Join(Environment.NewLine, lines);

        [Fact]
        public void Build_CreatesTreeWithSequentialIds()
        {
            // Arrange & Act
            var root = new DocumentBuilder("Doc")
                .AddParagraph("intro")
                .BeginHeading("Chapter")
                    .AddParagraph("text")
                .EndHeading()
                .Build();

            // Assert
            Assert.Equal(
                Join("[0] Title: Doc", "    [1] intro", "    [2] Chapter", "        [3] text"),
                new TextDto(root, true).Render());
        }

        [Fact]
        public void AddHeading_WithContent_NestsAndClosesAutomatically()
        {
            // Arrange & Act
            var root = new DocumentBuilder("Doc")
                .AddHeading("A", a => a.AddParagraph("p").AddHeading("B", b => b.AddParagraph("q")))
                .AddParagraph("after")
                .Build();

            // Assert
            Assert.Equal(
                Join("Title: Doc", "    A", "        p", "        B", "            q", "    after"),
                new TextDto(root, false).Render());
        }

        [Fact]
        public void Constructor_TrimsTitle()
        {
            // Act
            var root = new DocumentBuilder("  Doc ").Build();

            // Assert
            Assert.Equal("Doc", root.Name);
            Assert.Equal(0, root.Id);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_BlankTitle_Throws(string title)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new DocumentBuilder(title));
        }

        [Theory]
        [InlineData("")]
        [InlineData("a/b")]
        [InlineData(".")]
        [InlineData("..")]
        public void BeginHeading_InvalidName_Throws(string name)
        {
            // Arrange
            var builder = new DocumentBuilder("Doc");

            // Act & Assert
            Assert.Throws<ArgumentException>(() => builder.BeginHeading(name));
        }

        [Fact]
        public void FailedCall_DoesNotConsumeIdOrChangeStructure()
        {
            // Arrange
            var builder = new DocumentBuilder("Doc");
            Assert.Throws<ArgumentException>(() => builder.BeginHeading("a/b"));

            // Act
            var root = builder.AddParagraph("p").Build();

            // Assert
            Assert.Equal(1, root.Children[0].Id);
            Assert.Single(root.Children);
        }

        [Fact]
        public void AddParagraph_Blank_Throws()
        {
            // Arrange
            var builder = new DocumentBuilder("Doc");

            // Act & Assert
            Assert.Throws<ArgumentException>(() => builder.AddParagraph("  "));
        }

        [Fact]
        public void EndHeading_WithoutOpenHeading_Throws()
        {
            // Arrange
            var builder = new DocumentBuilder("Doc");

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => builder.EndHeading());
        }

        [Fact]
        public void Build_WithUnclosedHeading_ThrowsAndNamesIt()
        {
            // Arrange
            var builder = new DocumentBuilder("Doc").BeginHeading("Chapter");

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

            // Assert
            Assert.Contains("Chapter", exception.Message);
        }

        [Fact]
        public void AddHeading_NullContent_Throws()
        {
            // Arrange
            var builder = new DocumentBuilder("Doc");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.AddHeading("A", null!));
        }

        [Fact]
        public void AfterBuild_BuilderIsSpent()
        {
            // Arrange
            var builder = new DocumentBuilder("Doc");
            builder.Build();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => builder.AddParagraph("x"));
            Assert.Throws<InvalidOperationException>(() => builder.BeginHeading("A"));
            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Fact]
        public void BuiltDocument_CanBeLoadedIntoFactory_AndIdsContinue()
        {
            // Arrange
            var root = new DocumentBuilder("Doc")
                .AddParagraph("intro")
                .AddHeading("Chapter", c => c.AddParagraph("text"))
                .Build();
            var factory = new TextFactory("Other");

            // Act
            factory.Load(root);
            var next = factory.AddHeading("New");

            // Assert
            Assert.Equal(4, next.Id); // the builder used ids 1..3
            Assert.Equal("Doc", factory.Document.Name);
        }
    }

    public class PrototypeTests
    {
        /// <summary>Doc{ Chapter(1){ text(2), Inner(3){ deep(4) } } }, current = root.</summary>
        private static (TextFactory factory, Heading chapter) BuildDocument()
        {
            var factory = new TextFactory("Doc");
            var chapter = factory.AddHeading("Chapter");
            factory.AddParagraph("text");
            factory.AddHeading("Inner");
            factory.AddParagraph("deep");
            factory.TryCd("/", out _);
            return (factory, chapter);
        }

        private static List<int> Ids(IText node) =>
            node is Container container
                ? new[] { container.Id }.Concat(container.Children.SelectMany(child => Ids(child))).ToList()
                : new List<int> { node.Id };

        [Fact]
        public void Heading_Clone_IsDeepCopyWithFreshIds()
        {
            // Arrange
            var (_, chapter) = BuildDocument();
            int next = 100;

            // Act
            var copy = (Heading)chapter.Clone(() => next++);

            // Assert
            Assert.NotSame(chapter, copy);
            Assert.Equal("Chapter", copy.Name);
            Assert.Equal(new[] { 100, 101, 102, 103 }, Ids(copy));
            Assert.Equal(new[] { 1, 2, 3, 4 }, Ids(chapter));
            Assert.Null(copy.Parent);
        }

        [Fact]
        public void Heading_Clone_CopiesNamesOfNestedElements()
        {
            // Arrange
            var (_, chapter) = BuildDocument();

            // Act
            var copy = (Heading)chapter.Clone(() => 100);
            var inner = (Container)copy.Children[1];

            // Assert
            Assert.Equal("text", copy.Children[0].Name);
            Assert.Equal("Inner", inner.Name);
            Assert.Equal("deep", inner.Children[0].Name);
        }

        [Fact]
        public void Heading_Clone_IsIndependentFromTheOriginal()
        {
            // Arrange
            var (_, chapter) = BuildDocument();
            var copy = (Heading)chapter.Clone(() => 100);
            var originalInner = (Container)chapter.Children[1];
            var copyInner = (Container)copy.Children[1];

            // Act
            copyInner.RemoveChild(copyInner.Children[0]);

            // Assert
            Assert.Empty(copyInner.Children);
            Assert.Single(originalInner.Children);
        }

        [Fact]
        public void Heading_CloneAs_RenamesOnlyTheTopHeading()
        {
            // Arrange
            var (_, chapter) = BuildDocument();

            // Act
            var copy = chapter.CloneAs("Renamed", () => 100);

            // Assert
            Assert.Equal("Renamed", copy.Name);
            Assert.Equal("Inner", copy.Children[1].Name);
        }

        [Fact]
        public void Heading_CloneAs_NullIdSource_Throws()
        {
            // Arrange
            var (_, chapter) = BuildDocument();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => chapter.CloneAs("X", null!));
        }

        [Fact]
        public void Paragraph_Clone_KeepsContentAndTakesNewId()
        {
            // Arrange
            var original = new Paragraph(7, "hello");

            // Act
            var copy = original.Clone(() => 42);

            // Assert
            var paragraph = Assert.IsType<Paragraph>(copy);
            Assert.Equal("hello", paragraph.Content);
            Assert.Equal(42, paragraph.Id);
        }

        [Fact]
        public void AddCopy_Heading_AddsRenamedCopyToCurrentContainer()
        {
            // Arrange
            var (factory, chapter) = BuildDocument();

            // Act
            var copy = factory.AddCopy(chapter);

            // Assert
            Assert.Equal("Chapter (copy)", copy.Name);
            Assert.Equal(5, copy.Id);
            Assert.Equal(2, factory.Document.Children.Count);
            Assert.Same(copy, factory.Document.Children[1]);
            Assert.Equal("/", factory.GetCurrentPath());
        }

        [Fact]
        public void AddCopy_KeepsAllIdsInTheDocumentUnique()
        {
            // Arrange
            var (factory, chapter) = BuildDocument();

            // Act
            factory.AddCopy(chapter);
            var ids = Ids(factory.Document);

            // Assert
            Assert.Equal(9, ids.Count);
            Assert.Equal(9, ids.Distinct().Count());
        }

        [Fact]
        public void AddCopy_WithNewName_UsesTrimmedName()
        {
            // Arrange
            var (factory, chapter) = BuildDocument();

            // Act
            var copy = factory.AddCopy(chapter, " Second ");

            // Assert
            Assert.Equal("Second", copy.Name);
        }

        [Fact]
        public void AddCopy_InvalidName_ThrowsAndChangesNothing()
        {
            // Arrange
            var (factory, chapter) = BuildDocument();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => factory.AddCopy(chapter, "a/b"));
            Assert.Single(factory.Document.Children);
        }

        [Fact]
        public void AddCopy_Paragraph_CopiesContent()
        {
            // Arrange
            var (factory, _) = BuildDocument();
            factory.TryFindById(2, out var paragraph, out _);

            // Act
            var copy = factory.AddCopy(paragraph!);

            // Assert
            Assert.Equal("text", copy.Name);
            Assert.IsType<Paragraph>(copy);
            Assert.Equal(2, factory.Document.Children.Count);
        }

        [Fact]
        public void AddCopy_Root_IsRejected()
        {
            // Arrange
            var (factory, _) = BuildDocument();

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => factory.AddCopy(factory.Document));

            // Assert
            Assert.Equal("Root 'Doc' cannot be copied.", exception.Message);
        }

        [Fact]
        public void AddCopy_IntoTheCopiedHeadingItself_CopiesTheOriginalContentOnly()
        {
            // Arrange
            var (factory, chapter) = BuildDocument();
            factory.TryCd("/Chapter", out _);

            // Act
            var copy = (Heading)factory.AddCopy(chapter);

            // Assert
            Assert.Equal(3, chapter.Children.Count); // text, Inner and the copy
            Assert.Equal(2, copy.Children.Count);    // the copy was made before it was inserted
        }
    }

    public class FormattingTests
    {
        private static string Join(params string[] lines) => string.Join(Environment.NewLine, lines);

        private static string Render(ITextFormatFactory formats, bool showIds = false) =>
            new TextDto(TextSamples.BuildDocument().Document, showIds, formats).Render();

        // ---------- plain ----------

        [Fact]
        public void Plain_IsTheDefaultFormat()
        {
            // Arrange
            var root = TextSamples.BuildDocument().Document;

            // Act & Assert
            Assert.Equal(
                new TextDto(root, true).Render(),
                new TextDto(root, true, TextFormats.Plain).Render());
        }

        // ---------- html ----------

        [Fact]
        public void Html_RendersArticleSectionsAndParagraphs()
        {
            // Act
            string html = Render(new HtmlFormatFactory());

            // Assert
            Assert.Equal(
                Join("<article>",
                     "  <h1>Doc</h1>",
                     "  <p>p0</p>",
                     "  <section>",
                     "    <h2>H</h2>",
                     "    <p>p1</p>",
                     "  </section>",
                     "</article>"),
                html);
        }

        [Fact]
        public void Html_WithIds_WritesDataIdAttributes()
        {
            // Act
            string html = Render(new HtmlFormatFactory(), showIds: true);

            // Assert
            Assert.Equal(
                Join("<article data-id=\"0\">",
                     "  <h1>Doc</h1>",
                     "  <p data-id=\"1\">p0</p>",
                     "  <section data-id=\"2\">",
                     "    <h2>H</h2>",
                     "    <p data-id=\"3\">p1</p>",
                     "  </section>",
                     "</article>"),
                html);
        }

        [Fact]
        public void Html_EncodesSpecialCharacters()
        {
            // Arrange
            var root = new DocumentBuilder("A&B").AddParagraph("<b>Tom & Jerry</b>").Build();

            // Act
            string html = new TextDto(root, false, new HtmlFormatFactory()).Render();

            // Assert
            Assert.Contains("<h1>A&amp;B</h1>", html);
            Assert.Contains("<p>&lt;b&gt;Tom &amp; Jerry&lt;/b&gt;</p>", html);
        }

        [Fact]
        public void Html_EmptyDocument_HasNoEmptyLines()
        {
            // Arrange
            var root = new DocumentBuilder("Doc").Build();

            // Act
            string html = new TextDto(root, false, new HtmlFormatFactory()).Render();

            // Assert
            Assert.Equal(Join("<article>", "  <h1>Doc</h1>", "</article>"), html);
        }

        [Fact]
        public void Html_DeepNesting_NeverExceedsH6()
        {
            // Arrange
            var root = DeepDocument();

            // Act
            string html = new TextDto(root, false, new HtmlFormatFactory()).Render();

            // Assert
            Assert.Contains("<h6>H6</h6>", html);
            Assert.DoesNotContain("<h7>", html);
        }

        // ---------- markdown ----------

        [Fact]
        public void Markdown_SeparatesBlocksWithBlankLines()
        {
            // Act
            string markdown = Render(new MarkdownFormatFactory());

            // Assert
            Assert.Equal(Join("# Doc", "", "p0", "", "## H", "", "p1"), markdown);
        }

        [Fact]
        public void Markdown_WithIds_WritesComments()
        {
            // Act
            string markdown = Render(new MarkdownFormatFactory(), showIds: true);

            // Assert
            Assert.Equal(
                Join("# Doc <!-- id: 0 -->", "", "p0 <!-- id: 1 -->", "", "## H <!-- id: 2 -->", "", "p1 <!-- id: 3 -->"),
                markdown);
        }

        [Fact]
        public void Markdown_DeepNesting_NeverExceedsSixHashes()
        {
            // Arrange
            var root = DeepDocument();

            // Act
            string markdown = new TextDto(root, false, new MarkdownFormatFactory()).Render();

            // Assert
            Assert.Contains("###### H6", markdown);
            Assert.DoesNotContain("#######", markdown);
        }

        // ---------- registry ----------

        [Fact]
        public void TextFormats_ListsAllFormats()
        {
            // Act & Assert
            Assert.Equal(new[] { "plain", "html", "markdown" }, TextFormats.Names);
            Assert.Equal("plain", TextFormats.Plain.Name);
        }

        [Theory]
        [InlineData("html", "html")]
        [InlineData("HTML", "html")]
        [InlineData("Markdown", "markdown")]
        public void TextFormats_TryGet_FindsByNameIgnoringCase(string name, string expected)
        {
            // Act
            bool found = TextFormats.TryGet(name, out var factory);

            // Assert
            Assert.True(found);
            Assert.Equal(expected, factory!.Name);
        }

        [Fact]
        public void TextFormats_TryGet_UnknownName_ReturnsFalse()
        {
            // Act
            bool found = TextFormats.TryGet("pdf", out var factory);

            // Assert
            Assert.False(found);
            Assert.Null(factory);
        }

        [Fact]
        public void EveryFactory_CreatesAFullFamily()
        {
            // Act & Assert
            foreach (string name in TextFormats.Names)
            {
                TextFormats.TryGet(name, out var factory);
                Assert.NotNull(factory!.CreateRootFormatter());
                Assert.NotNull(factory.CreateHeadingFormatter());
                Assert.NotNull(factory.CreateParagraphFormatter());
            }
        }

        // ---------- integration with the facade ----------

        [Fact]
        public void TextFactory_Views_AcceptAFormat()
        {
            // Arrange
            var factory = TextSamples.BuildDocument(); // current = H

            // Act
            string whole = factory.GetWholeView(false, new HtmlFormatFactory()).Render();
            string current = factory.GetCurrentView(false, new MarkdownFormatFactory()).Render();

            // Assert
            Assert.StartsWith("<article>", whole);
            Assert.Equal(Join("# H", "", "p1"), current);
        }

        // ---------- extensibility ----------

        [Fact]
        public void NewFormat_CanBePluggedInWithoutChangingTheDto()
        {
            // Arrange
            var root = TextSamples.BuildDocument().Document;

            // Act
            string text = new TextDto(root, false, new BracketFormatFactory()).Render();

            // Assert
            Assert.Equal("[Doc:p0,(H:p1)]", text);
        }

        private static Root DeepDocument()
        {
            var builder = new DocumentBuilder("Doc");
            for (int i = 1; i <= 6; i++)
            {
                builder.BeginHeading($"H{i}");
            }
            builder.AddParagraph("deep");
            for (int i = 1; i <= 6; i++)
            {
                builder.EndHeading();
            }
            return builder.Build();
        }

        /// <summary>A custom family defined outside of the system: a one-line bracket notation.</summary>
        private sealed class BracketFormatFactory : ITextFormatFactory
        {
            public string Name => "brackets";
            public string ChildSeparator => ",";

            public IRootFormatter CreateRootFormatter() => new RootFormatter();
            public IHeadingFormatter CreateHeadingFormatter() => new HeadingFormatter();
            public IParagraphFormatter CreateParagraphFormatter() => new ParagraphFormatter();

            private static string Wrap(string name, string children, string open, string close) =>
                open + name + (children.Length > 0 ? ":" + children : string.Empty) + close;

            private sealed class RootFormatter : IRootFormatter
            {
                public string Format(Root root, string renderedChildren, bool showIds) =>
                    Wrap(root.Name, renderedChildren, "[", "]");
            }

            private sealed class HeadingFormatter : IHeadingFormatter
            {
                public string Format(Heading heading, int level, string renderedChildren, bool showIds) =>
                    Wrap(heading.Name, renderedChildren, "(", ")");
            }

            private sealed class ParagraphFormatter : IParagraphFormatter
            {
                public string Format(Paragraph paragraph, int level, bool showIds) => paragraph.Content;
            }
        }
    }
}
