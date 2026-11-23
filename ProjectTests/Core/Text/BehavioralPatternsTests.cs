using System;
using System.Collections.Generic;
using System.Linq;
using GameSystem.Core.Dto;
using OOPTextProject.TextSystem;
using OOPTextProject.TextSystem.Building;
using ProjectTests.TextTests;
using Xunit;

namespace ProjectTests.BehaviorTests
{
    public static class PatternSamples
    {
        /// <summary>Doc{ p0(1), H(2){ p1(3), Inner(4){ deep(5) } }, tail(6) }</summary>
        public static Root BuildTree() => new DocumentBuilder("Doc")
            .AddParagraph("p0")
            .AddHeading("H", h => h.AddParagraph("p1").AddHeading("Inner", i => i.AddParagraph("deep")))
            .AddParagraph("tail")
            .Build();
    }

    // ---------- Iterator ----------

    public class IteratorTests
    {
        [Fact]
        public void SelfAndDescendants_WalksTheWholeSubtreeDepthFirst()
        {
            // Arrange
            var root = PatternSamples.BuildTree();

            // Act
            var ids = root.SelfAndDescendants().Select(node => node.Id).ToList();

            // Assert
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6 }, ids);
        }

        [Fact]
        public void SelfAndDescendants_StartsFromTheContainerItself()
        {
            // Arrange
            var root = PatternSamples.BuildTree();
            var heading = (Container)root.Children[1];

            // Act
            var ids = heading.SelfAndDescendants().Select(node => node.Id);

            // Assert
            Assert.Equal(new[] { 2, 3, 4, 5 }, ids);
        }

        [Fact]
        public void SelfAndDescendants_OfAnEmptyContainer_IsJustTheContainer()
        {
            // Arrange
            var heading = new Heading(1, "empty");

            // Act & Assert
            Assert.Same(heading, Assert.Single(heading.SelfAndDescendants()));
        }

        [Fact]
        public void SelfAndDescendants_IsLazy()
        {
            // Arrange
            var root = new Root("Doc");
            var walk = root.SelfAndDescendants();

            // Act: the walk has not started yet, so it sees the element added after it was created
            root.AddChild(new Paragraph(9, "late"));

            // Assert
            Assert.Contains(walk, node => node.Id == 9);
        }
    }

    // ---------- Visitor ----------

    public class VisitorTests
    {
        private sealed class KindVisitor : ITextVisitor<string>
        {
            public string VisitRoot(Root root) => "root";

            public string VisitHeading(Heading heading) => "heading";

            public string VisitParagraph(Paragraph paragraph) => "paragraph";
        }

        private sealed class CountVisitor : ITextVisitor<int>
        {
            public int VisitRoot(Root root) => 1 + CountChildren(root);

            public int VisitHeading(Heading heading) => 1 + CountChildren(heading);

            public int VisitParagraph(Paragraph paragraph) => 1;

            private int CountChildren(Container container) => container.Children.Sum(child => child.Accept(this));
        }

        [Fact]
        public void Accept_CallsTheMethodOfTheElementKind()
        {
            // Arrange
            var visitor = new KindVisitor();

            // Act & Assert
            Assert.Equal("root", new Root("r").Accept(visitor));
            Assert.Equal("heading", new Heading(1, "h").Accept(visitor));
            Assert.Equal("paragraph", new Paragraph(2, "p").Accept(visitor));
        }

        [Fact]
        public void AVisitorCanWalkTheWholeTree()
        {
            // Arrange
            var root = PatternSamples.BuildTree();

            // Act
            int count = root.Accept(new CountVisitor());

            // Assert
            Assert.Equal(7, count);
        }
    }

    // ---------- Memento ----------

    public class MementoTests
    {
        [Fact]
        public void Restore_BringsBackTheDocumentAndThePosition()
        {
            // Arrange
            var factory = TextSamples.BuildDocument(); // Doc{ p0(1), H(2){ p1(3) } }, current = H
            string documentBefore = factory.GetWholeView(true).Render();
            TextMemento memento = factory.CreateMemento();

            factory.AddParagraph("extra");
            factory.Up();
            factory.AddHeading("Another");

            // Act
            factory.Restore(memento);

            // Assert
            Assert.Equal(documentBefore, factory.GetWholeView(true).Render());
            Assert.Equal("/H", factory.GetCurrentPath());
        }

        [Fact]
        public void TheSameMementoCanBeRestoredAgain()
        {
            // Arrange
            var factory = TextSamples.BuildDocument();
            string documentBefore = factory.GetWholeView(true).Render();
            TextMemento memento = factory.CreateMemento();

            factory.AddParagraph("one");
            factory.Restore(memento);
            factory.AddParagraph("two");

            // Act
            factory.Restore(memento);

            // Assert
            Assert.Equal(documentBefore, factory.GetWholeView(true).Render());
        }

        [Fact]
        public void Restore_ContinuesIdsAfterTheRestoredMaximum()
        {
            // Arrange
            var factory = TextSamples.BuildDocument(); // the largest id is 3
            TextMemento memento = factory.CreateMemento();
            factory.AddParagraph("extra");
            factory.AddParagraph("more");

            // Act
            factory.Restore(memento);
            var next = factory.AddParagraph("new");

            // Assert
            Assert.Equal(4, next.Id);
        }

        [Fact]
        public void Restore_Null_Throws()
        {
            // Arrange
            var factory = new TextFactory("Doc");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => factory.Restore(null!));
        }

        [Fact]
        public void Revision_CountsChangesOfTheDocumentButNotNavigation()
        {
            // Arrange
            var factory = new TextFactory("Doc");
            Assert.Equal(0, factory.Revision);

            // Act & Assert
            var chapter = factory.AddHeading("A");        // 1
            factory.AddParagraph("p");                     // 2
            factory.Up();                                  // navigation
            factory.TryCd("/A", out _);                    // navigation
            Assert.Equal(2, factory.Revision);

            factory.TryCd("/", out _);
            var copy = factory.AddCopy(chapter);           // 3
            factory.TryRemove(copy, out _);                // 4
            factory.TryRemove(factory.Document, out _);    // rejected: the root cannot be removed
            Assert.Equal(4, factory.Revision);

            factory.Load(new Root("New"));                 // 5
            Assert.Equal(5, factory.Revision);
        }

        [Fact]
        public void Revision_IsNotChangedByRejectedOperations()
        {
            // Arrange
            var factory = new TextFactory("Doc");

            // Act
            Assert.Throws<ArgumentException>(() => factory.AddHeading("a/b"));
            Assert.Throws<ArgumentException>(() => factory.AddParagraph("  "));

            // Assert
            Assert.Equal(0, factory.Revision);
        }
    }

    public class UndoHistoryTests
    {
        private static (TextMemento first, TextMemento second, TextMemento third) Mementos()
        {
            var factory = new TextFactory("Doc");
            var first = factory.CreateMemento();
            factory.AddParagraph("a");
            var second = factory.CreateMemento();
            factory.AddParagraph("b");
            var third = factory.CreateMemento();
            return (first, second, third);
        }

        [Fact]
        public void TryPop_GivesTheLastSnapshotFirst()
        {
            // Arrange
            var (first, second, _) = Mementos();
            var history = new UndoHistory();
            history.Push(first);
            history.Push(second);

            // Act & Assert
            Assert.True(history.TryPop(out var popped));
            Assert.Same(second, popped);
            Assert.True(history.TryPop(out popped));
            Assert.Same(first, popped);
            Assert.False(history.TryPop(out popped));
            Assert.Null(popped);
        }

        [Fact]
        public void WhenFull_TheOldestSnapshotIsForgotten()
        {
            // Arrange
            var (first, second, third) = Mementos();
            var history = new UndoHistory(2);

            // Act
            history.Push(first);
            history.Push(second);
            history.Push(third);

            // Assert
            Assert.Equal(2, history.Count);
            history.TryPop(out var popped);
            Assert.Same(third, popped);
            history.TryPop(out popped);
            Assert.Same(second, popped);
            Assert.False(history.TryPop(out _));
        }

        [Fact]
        public void Capacity_MustBePositive()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => new UndoHistory(0));
        }

        [Fact]
        public void Push_Null_Throws()
        {
            // Arrange
            var history = new UndoHistory();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => history.Push(null!));
        }

        [Fact]
        public void Clear_ForgetsEverything()
        {
            // Arrange
            var (first, second, _) = Mementos();
            var history = new UndoHistory();
            history.Push(first);
            history.Push(second);

            // Act
            history.Clear();

            // Assert
            Assert.Equal(0, history.Count);
            Assert.False(history.TryPop(out _));
        }
    }

    // ---------- Template Method ----------

    public class TemplateMethodTests
    {
        private sealed class FakeDto : EntityDto
        {
            public FakeDto(bool detailed) : base(detailed) { }

            protected override string ShortLine() => "short";

            protected override IEnumerable<string> DetailLines()
            {
                yield return "first";
                yield return "second";
            }
        }

        [Fact]
        public void Render_UsesTheShortStep_WhenNotDetailed()
        {
            // Act & Assert
            Assert.Equal("short", new FakeDto(false).Render());
        }

        [Fact]
        public void Render_JoinsTheDetailLines_WhenDetailed()
        {
            // Act & Assert
            Assert.Equal("first" + Environment.NewLine + "second", new FakeDto(true).Render());
        }
    }
}