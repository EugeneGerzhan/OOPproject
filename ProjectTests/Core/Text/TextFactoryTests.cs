using System;
using OOPTextProject.TextSystem;
using Xunit;

namespace ProjectTests.TextTests
{
    public class TextFactoryTests
    {
        // ---------- creation ----------

        [Fact]
        public void NewFactory_StartsInRootWithZeroId()
        {
            // Arrange & Act
            var factory = new TextFactory("Doc");

            // Assert
            Assert.Equal("/", factory.GetCurrentPath());
            Assert.True(factory.Current.IsRoot());
            Assert.Equal(0, factory.Document.Id);
            Assert.Equal("Doc", factory.Document.Name);
        }

        [Fact]
        public void AddHeading_EntersTheNewHeading()
        {
            // Arrange
            var factory = new TextFactory("Doc");

            // Act
            var heading = factory.AddHeading("Intro");

            // Assert
            Assert.Same(heading, factory.Current);
            Assert.Equal("/Intro", factory.GetCurrentPath());
        }

        [Fact]
        public void AddHeading_Nested_BuildsFullPath()
        {
            // Arrange
            var factory = new TextFactory("Doc");

            // Act
            factory.AddHeading("A");
            factory.AddHeading("B");

            // Assert
            Assert.Equal("/A/B", factory.GetCurrentPath());
        }

        [Fact]
        public void AddHeading_TrimsTitle()
        {
            // Arrange
            var factory = new TextFactory("Doc");

            // Act
            var heading = factory.AddHeading("  Intro  ");

            // Assert
            Assert.Equal("Intro", heading.Name);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("a/b")]
        [InlineData(".")]
        [InlineData("..")]
        public void AddHeading_InvalidTitle_Throws(string title)
        {
            // Arrange
            var factory = new TextFactory("Doc");

            // Act & Assert
            Assert.Throws<ArgumentException>(() => factory.AddHeading(title));
            Assert.Empty(factory.Document.Children);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void AddParagraph_EmptyContent_Throws(string content)
        {
            // Arrange
            var factory = new TextFactory("Doc");

            // Act & Assert
            Assert.Throws<ArgumentException>(() => factory.AddParagraph(content));
        }

        [Fact]
        public void AddParagraph_AddsToCurrentAndDoesNotMove()
        {
            // Arrange
            var factory = new TextFactory("Doc");

            // Act
            var paragraph = factory.AddParagraph("text");

            // Assert
            Assert.Single(factory.Document.Children);
            Assert.Same(paragraph, factory.Document.Children[0]);
            Assert.Equal("/", factory.GetCurrentPath());
        }

        [Fact]
        public void NewElements_GetIncreasingUniqueIds()
        {
            // Arrange
            var factory = new TextFactory("Doc");

            // Act
            var paragraph = factory.AddParagraph("p");
            var heading = factory.AddHeading("H");

            // Assert
            Assert.Equal(1, paragraph.Id);
            Assert.Equal(2, heading.Id);
        }

        // ---------- up ----------

        [Fact]
        public void Up_InRoot_ReturnsFalse()
        {
            // Arrange
            var factory = new TextFactory("Doc");

            // Act
            bool moved = factory.Up();

            // Assert
            Assert.False(moved);
            Assert.Equal("/", factory.GetCurrentPath());
        }

        [Fact]
        public void Up_InsideHeading_MovesToParent()
        {
            // Arrange
            var factory = new TextFactory("Doc");
            factory.AddHeading("A");

            // Act
            bool moved = factory.Up();

            // Assert
            Assert.True(moved);
            Assert.Equal("/", factory.GetCurrentPath());
        }

        // ---------- cd by path ----------

        [Fact]
        public void TryCd_AbsolutePath_MovesToNestedContainer()
        {
            // Arrange
            var factory = new TextFactory("Doc");
            factory.AddHeading("A");
            factory.AddHeading("B");
            factory.TryCd("/", out _);

            // Act
            bool ok = factory.TryCd("/A/B", out var error);

            // Assert
            Assert.True(ok);
            Assert.Equal(string.Empty, error);
            Assert.Equal("/A/B", factory.GetCurrentPath());
        }

        [Fact]
        public void TryCd_RelativePath_StartsFromCurrent()
        {
            // Arrange
            var factory = new TextFactory("Doc");
            factory.AddHeading("A");
            factory.AddHeading("B");
            factory.TryCd("/", out _);

            // Act
            bool ok = factory.TryCd("A/B", out _);

            // Assert
            Assert.True(ok);
            Assert.Equal("/A/B", factory.GetCurrentPath());
        }

        [Fact]
        public void TryCd_DoubleDot_MovesUp()
        {
            // Arrange
            var factory = new TextFactory("Doc");
            factory.AddHeading("A");
            factory.AddHeading("B");

            // Act
            bool ok = factory.TryCd("..", out _);

            // Assert
            Assert.True(ok);
            Assert.Equal("/A", factory.GetCurrentPath());
        }

        [Fact]
        public void TryCd_IsCaseInsensitive()
        {
            // Arrange
            var factory = new TextFactory("Doc");
            factory.AddHeading("Chapter");
            factory.Up();

            // Act
            bool ok = factory.TryCd("/chAPTER", out _);

            // Assert
            Assert.True(ok);
            Assert.Equal("/Chapter", factory.GetCurrentPath());
        }

        [Fact]
        public void TryCd_MissingContainer_FailsAndKeepsPosition()
        {
            // Arrange
            var factory = new TextFactory("Doc");
            factory.AddHeading("A");

            // Act
            bool ok = factory.TryCd("Missing", out var error);

            // Assert
            Assert.False(ok);
            Assert.Contains("not found", error);
            Assert.Equal("/A", factory.GetCurrentPath());
        }

        [Fact]
        public void TryCd_PartiallyResolvedPath_DoesNotMove()
        {
            // Arrange
            var factory = new TextFactory("Doc");
            factory.AddHeading("A");
            factory.Up();

            // Act
            bool ok = factory.TryCd("/A/Missing", out _);

            // Assert
            Assert.False(ok);
            Assert.Equal("/", factory.GetCurrentPath());
        }

        [Fact]
        public void TryCd_AmbiguousName_FailsAndAsksForId()
        {
            // Arrange
            var factory = new TextFactory("Doc");
            factory.AddHeading("Dup");
            factory.Up();
            factory.AddHeading("Dup");
            factory.Up();

            // Act
            bool ok = factory.TryCd("Dup", out var error);

            // Assert
            Assert.False(ok);
            Assert.Contains("Several", error);
        }

        [Fact]
        public void TryCd_EmptyPath_Fails()
        {
            // Arrange
            var factory = new TextFactory("Doc");

            // Act
            bool ok = factory.TryCd("  ", out var error);

            // Assert
            Assert.False(ok);
            Assert.Equal("Path cannot be empty.", error);
        }

        // ---------- cd by id ----------

        [Fact]
        public void TryCdById_Container_MovesThere()
        {
            // Arrange
            var factory = new TextFactory("Doc");
            var heading = factory.AddHeading("A");
            factory.Up();

            // Act
            bool ok = factory.TryCdById(heading.Id, out _);

            // Assert
            Assert.True(ok);
            Assert.Equal("/A", factory.GetCurrentPath());
        }

        [Fact]
        public void TryCdById_UnknownId_Fails()
        {
            // Arrange
            var factory = new TextFactory("Doc");

            // Act
            bool ok = factory.TryCdById(999, out var error);

            // Assert
            Assert.False(ok);
            Assert.Contains("not found", error);
        }

        [Fact]
        public void TryCdById_Paragraph_FailsBecauseNotContainer()
        {
            // Arrange
            var factory = new TextFactory("Doc");
            var paragraph = factory.AddParagraph("text");

            // Act
            bool ok = factory.TryCdById(paragraph.Id, out var error);

            // Assert
            Assert.False(ok);
            Assert.Contains("not a container", error);
        }

        // ---------- find / remove ----------

        [Fact]
        public void TryFindChild_ByName_IsCaseInsensitive()
        {
            // Arrange
            var factory = new TextFactory("Doc");
            factory.AddHeading("A");
            factory.Up();

            // Act
            bool found = factory.TryFindChild("a", out var element, out _);

            // Assert
            Assert.True(found);
            Assert.Equal("A", element!.Name);
        }

        [Fact]
        public void TryFindChild_Missing_Fails()
        {
            // Arrange
            var factory = new TextFactory("Doc");

            // Act
            bool found = factory.TryFindChild("nothing", out var element, out var error);

            // Assert
            Assert.False(found);
            Assert.Null(element);
            Assert.Contains("not found", error);
        }

        [Fact]
        public void TryFindChild_SeveralWithSameName_Fails()
        {
            // Arrange
            var factory = new TextFactory("Doc");
            factory.AddParagraph("same");
            factory.AddParagraph("same");

            // Act
            bool found = factory.TryFindChild("same", out _, out var error);

            // Assert
            Assert.False(found);
            Assert.Contains("Several", error);
        }

        [Fact]
        public void TryFindById_RootHasIdZero()
        {
            // Arrange
            var factory = new TextFactory("Doc");

            // Act
            bool found = factory.TryFindById(0, out var element, out _);

            // Assert
            Assert.True(found);
            Assert.Same(factory.Document, element);
        }

        [Fact]
        public void TryRemove_Child_RemovesItFromDocument()
        {
            // Arrange
            var factory = new TextFactory("Doc");
            factory.AddHeading("A");
            factory.Up();
            factory.TryFindChild("A", out var element, out _);

            // Act
            bool removed = factory.TryRemove(element!, out _);

            // Assert
            Assert.True(removed);
            Assert.Empty(factory.Document.Children);
        }

        [Fact]
        public void TryRemove_CurrentContainer_MovesToParent()
        {
            // Arrange
            var factory = new TextFactory("Doc");
            factory.AddHeading("A");

            // Act
            bool removed = factory.TryRemove(factory.Current, out _);

            // Assert
            Assert.True(removed);
            Assert.Equal("/", factory.GetCurrentPath());
            Assert.Empty(factory.Document.Children);
        }

        [Fact]
        public void TryRemove_AncestorOfCurrent_MovesToItsParent()
        {
            // Arrange
            var factory = new TextFactory("Doc");
            var a = factory.AddHeading("A");
            factory.AddHeading("B");

            // Act
            bool removed = factory.TryRemove(a, out _);

            // Assert
            Assert.True(removed);
            Assert.Equal("/", factory.GetCurrentPath());
        }

        [Fact]
        public void TryRemove_Root_Fails()
        {
            // Arrange
            var factory = new TextFactory("Doc");

            // Act
            bool removed = factory.TryRemove(factory.Document, out var error);

            // Assert
            Assert.False(removed);
            Assert.Equal("Root cannot be removed.", error);
        }

        // ---------- load / views ----------

        [Fact]
        public void Load_ReplacesDocument_AndContinuesIdsAfterMaximum()
        {
            // Arrange
            var factory = new TextFactory("Old");
            var root = new Root("Loaded");
            root.AddChild(new Heading(5, "H"));

            // Act
            factory.Load(root);
            var next = factory.AddHeading("N");

            // Assert
            Assert.Same(root, factory.Document);
            Assert.Equal(6, next.Id);
        }

        [Fact]
        public void Load_Null_Throws()
        {
            // Arrange
            var factory = new TextFactory("Doc");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => factory.Load(null!));
        }

        [Fact]
        public void ToString_EqualsRenderedWholeView()
        {
            // Arrange
            var factory = new TextFactory("Doc");
            factory.AddHeading("A");

            // Act
            string text = factory.ToString();

            // Assert
            Assert.Equal(factory.GetWholeView().Render(), text);
        }
    }
}
