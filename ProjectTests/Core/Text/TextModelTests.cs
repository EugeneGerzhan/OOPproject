using System;
using System.Collections.Generic;
using System.IO;
using Infra.Data;
using OOPTextProject.TextSystem;
using Xunit;

namespace ProjectTests.TextTests
{
    /// <summary>Document used by several test classes: Doc{ p0(1), H(2){ p1(3) } }, current = H.</summary>
    public static class TextSamples
    {
        public static TextFactory BuildDocument()
        {
            var factory = new TextFactory("Doc");
            factory.AddParagraph("p0");
            factory.AddHeading("H");
            factory.AddParagraph("p1");
            return factory;
        }
    }

    public class ContainerTests
    {
        private static (Root root, Heading heading, Paragraph paragraph) BuildTree()
        {
            var root = new Root("Doc");
            var heading = new Heading(1, "H");
            var paragraph = new Paragraph(2, "p");
            heading.AddChild(paragraph);
            root.AddChild(heading);
            return (root, heading, paragraph);
        }

        [Fact]
        public void AddChild_SetsParentOfContainer()
        {
            // Arrange & Act
            var (root, heading, _) = BuildTree();

            // Assert
            Assert.Same(root, heading.Parent);
            Assert.True(root.IsRoot());
            Assert.False(heading.IsRoot());
        }

        [Fact]
        public void Paragraph_NameAndContentAreTheText()
        {
            // Arrange & Act
            var paragraph = new Paragraph(7, "hello");

            // Assert
            Assert.Equal("hello", paragraph.Name);
            Assert.Equal("hello", paragraph.Content);
            Assert.Equal(7, paragraph.Id);
        }

        [Fact]
        public void FindById_FindsNestedElement()
        {
            // Arrange
            var (root, _, paragraph) = BuildTree();

            // Act
            var found = root.FindById(2);

            // Assert
            Assert.Same(paragraph, found);
        }

        [Fact]
        public void FindById_OwnId_ReturnsItself()
        {
            // Arrange
            var (root, _, _) = BuildTree();

            // Act & Assert
            Assert.Same(root, root.FindById(0));
        }

        [Fact]
        public void FindById_UnknownId_ReturnsNull()
        {
            // Arrange
            var (root, _, _) = BuildTree();

            // Act & Assert
            Assert.Null(root.FindById(99));
        }

        [Fact]
        public void FindParentOf_ReturnsDirectHolder()
        {
            // Arrange
            var (root, heading, _) = BuildTree();

            // Act & Assert
            Assert.Same(heading, root.FindParentOf(2));
            Assert.Same(root, root.FindParentOf(1));
            Assert.Null(root.FindParentOf(0));
        }

        [Fact]
        public void RemoveChild_RemovesAndClearsParent()
        {
            // Arrange
            var (root, heading, _) = BuildTree();

            // Act
            bool first = root.RemoveChild(heading);
            bool second = root.RemoveChild(heading);

            // Assert
            Assert.True(first);
            Assert.False(second);
            Assert.Null(heading.Parent);
            Assert.Empty(root.Children);
        }
    }

    public class TextDtoTests
    {
        private static string Join(params string[] lines) => string.Join(Environment.NewLine, lines);

        [Fact]
        public void Render_WholeDocument_IndentsByLevel()
        {
            // Arrange
            var factory = TextSamples.BuildDocument();

            // Act
            string text = factory.GetWholeView(false).Render();

            // Assert
            Assert.Equal(Join("Title: Doc", "    p0", "    H", "        p1"), text);
        }

        [Fact]
        public void Render_WholeDocument_WithIds()
        {
            // Arrange
            var factory = TextSamples.BuildDocument();

            // Act
            string text = factory.GetWholeView(true).Render();

            // Assert
            Assert.Equal(Join("[0] Title: Doc", "    [1] p0", "    [2] H", "        [3] p1"), text);
        }

        [Fact]
        public void Render_CurrentView_StartsFromCurrentElementWithoutIndent()
        {
            // Arrange
            var factory = TextSamples.BuildDocument(); // current = H

            // Act
            string text = factory.GetCurrentView(true).Render();

            // Assert
            Assert.Equal(Join("[2] H", "    [3] p1"), text);
        }

        [Fact]
        public void Render_SingleParagraph_IsJustItsText()
        {
            // Arrange
            var dto = new TextDto(new Paragraph(1, "hello"), false);

            // Act & Assert
            Assert.Equal("hello", dto.Render());
        }

        [Fact]
        public void Render_EmptyDocument_IsOnlyTheTitle()
        {
            // Arrange
            var factory = new TextFactory("Empty");

            // Act & Assert
            Assert.Equal("Title: Empty", factory.GetWholeView().Render());
        }
    }

    public class TextMapperTests
    {
        [Fact]
        public void ToSave_StoresNameAndTreeStructure()
        {
            // Arrange
            var factory = TextSamples.BuildDocument();

            // Act
            var save = TextMapper.ToSave("s1", factory.Document);

            // Assert
            Assert.Equal("s1", save.SaveName);
            var rootData = save.TextRoot!;
            Assert.Equal(TextNodeKinds.Root, rootData.Kind);
            Assert.Equal("Doc", rootData.Text);
            Assert.Equal(2, rootData.Children!.Count);
            Assert.Equal(TextNodeKinds.Paragraph, rootData.Children[0].Kind);
            Assert.Equal(TextNodeKinds.Heading, rootData.Children[1].Kind);
            Assert.Equal("p1", rootData.Children[1].Children![0].Text);
        }

        [Fact]
        public void RoundTrip_PreservesStructureAndIds()
        {
            // Arrange
            var factory = TextSamples.BuildDocument();
            var save = TextMapper.ToSave("s1", factory.Document);

            // Act
            var restored = TextMapper.ToRoot(save);

            // Assert
            Assert.Equal(
                new TextDto(factory.Document, true).Render(),
                new TextDto(restored, true).Render());
        }

        [Fact]
        public void ToRoot_NoRoot_Throws()
        {
            // Arrange
            var save = new TextSave { SaveName = "x" };

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => TextMapper.ToRoot(save));
        }

        [Fact]
        public void ToRoot_RootOfWrongKind_Throws()
        {
            // Arrange
            var save = new TextSave { TextRoot = new TextNodeData(0, TextNodeKinds.Heading, "x") };

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => TextMapper.ToRoot(save));
        }

        [Fact]
        public void ToRoot_DuplicateIds_Throws()
        {
            // Arrange
            var save = new TextSave
            {
                TextRoot = new TextNodeData(0, TextNodeKinds.Root, "Doc", new List<TextNodeData>
                {
                    new TextNodeData(1, TextNodeKinds.Paragraph, "a"),
                    new TextNodeData(1, TextNodeKinds.Paragraph, "b")
                })
            };

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => TextMapper.ToRoot(save));
        }

        [Fact]
        public void ToRoot_ChildWithRootId_Throws()
        {
            // Arrange
            var save = new TextSave
            {
                TextRoot = new TextNodeData(0, TextNodeKinds.Root, "Doc", new List<TextNodeData>
                {
                    new TextNodeData(0, TextNodeKinds.Paragraph, "a")
                })
            };

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => TextMapper.ToRoot(save));
        }

        [Fact]
        public void ToRoot_UnknownKind_Throws()
        {
            // Arrange
            var save = new TextSave
            {
                TextRoot = new TextNodeData(0, TextNodeKinds.Root, "Doc", new List<TextNodeData>
                {
                    new TextNodeData(1, "table", "a")
                })
            };

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => TextMapper.ToRoot(save));
        }
    }
}