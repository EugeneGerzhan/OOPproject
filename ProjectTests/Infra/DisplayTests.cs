using System;
using System.IO;
using Infra.Display;
using Moq;
using ProjectTests.Mocks;
using Xunit;

namespace ProjectTests.InfraTests
{
    /// <summary>Tests that touch the real Console. One class, so they never run in parallel with each other.</summary>
    public class ConsoleTests : IDisposable
    {
        private readonly TextWriter _originalOut = Console.Out;
        private readonly TextReader _originalIn = Console.In;

        public void Dispose()
        {
            Console.SetOut(_originalOut);
            Console.SetIn(_originalIn);
        }

        [Fact]
        public void ConsoleDisplayer_DisplayString_WritesLine()
        {
            // Arrange
            var output = new StringWriter();
            Console.SetOut(output);
            var displayer = new ConsoleDisplayer();

            // Act
            displayer.Display("hello");

            // Assert
            Assert.Equal("hello" + Environment.NewLine, output.ToString());
        }

        [Fact]
        public void ConsoleDisplayer_DisplayDisplayable_WritesRenderedText()
        {
            // Arrange
            var output = new StringWriter();
            Console.SetOut(output);
            var displayable = new Mock<IDisplayable>();
            displayable.Setup(d => d.Render()).Returns("rendered");
            var displayer = new ConsoleDisplayer();

            // Act
            displayer.Display(displayable.Object);

            // Assert
            Assert.Equal("rendered" + Environment.NewLine, output.ToString());
        }

        [Fact]
        public void ConsoleDisplayer_Write_DoesNotAddLineBreak()
        {
            // Arrange
            var output = new StringWriter();
            Console.SetOut(output);
            var displayer = new ConsoleDisplayer();

            // Act
            displayer.Write("prompt> ");

            // Assert
            Assert.Equal("prompt> ", output.ToString());
        }

        [Fact]
        public void ConsoleInputReader_ReadsLinesThenNull()
        {
            // Arrange
            Console.SetIn(new StringReader("first" + Environment.NewLine + "second"));
            var reader = new ConsoleInputReader();

            // Act
            var first = reader.ReadLine();
            var second = reader.ReadLine();
            var third = reader.ReadLine();

            // Assert
            Assert.Equal("first", first);
            Assert.Equal("second", second);
            Assert.Null(third);
        }
    }

    public class DisplayDoublesTests
    {
        [Fact]
        public void DisplayerMock_RecordsLinesAndDisplayables()
        {
            // Arrange
            var mock = new DisplayerMock();
            var displayable = new Mock<IDisplayable>();
            displayable.Setup(d => d.Render()).Returns("view");

            // Act
            mock.Display("line");
            mock.Display(displayable.Object);
            mock.Write("prompt");

            // Assert
            Assert.Equal(1, mock.DisplayLineCount);
            Assert.Equal(1, mock.DisplayCount);
            Assert.Equal(new[] { "line", "view" }, mock.Lines);
            Assert.Equal(new[] { "prompt" }, mock.Writes);
        }

        [Fact]
        public void DisplayerMock_Reset_ClearsEverything()
        {
            // Arrange
            var mock = new DisplayerMock();
            mock.Display("line");
            mock.Write("prompt");

            // Act
            mock.Reset();

            // Assert
            Assert.Empty(mock.Lines);
            Assert.Empty(mock.Writes);
            Assert.Equal(0, mock.DisplayLineCount);
        }

        [Fact]
        public void DisplayerStub_ThrowsOnlyForBadLine()
        {
            // Arrange
            var stub = new DisplayerStub("bad", Mock.Of<IDisplayable>());

            // Act
            var good = Record.Exception(() => stub.Display("good"));
            var bad = Record.Exception(() => stub.Display("bad"));

            // Assert
            Assert.Null(good);
            Assert.NotNull(bad);
        }

        [Fact]
        public void DisplayerStub_ThrowsOnlyForBadDisplayable()
        {
            // Arrange
            var badDisplayable = Mock.Of<IDisplayable>();
            var stub = new DisplayerStub("bad", badDisplayable);

            // Act
            var good = Record.Exception(() => stub.Display(Mock.Of<IDisplayable>()));
            var bad = Record.Exception(() => stub.Display(badDisplayable));

            // Assert
            Assert.Null(good);
            Assert.NotNull(bad);
        }

        [Fact]
        public void DisplayerFake_IgnoresEverything()
        {
            // Arrange
            var fake = new DisplayerFake();

            // Act
            var exception = Record.Exception(() =>
            {
                fake.Display("x");
                fake.Display(Mock.Of<IDisplayable>());
                fake.Write("y");
            });

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void InputReaderStub_ReturnsScriptedAnswersThenNull()
        {
            // Arrange
            var input = new InputReaderStub("a", "b");

            // Act & Assert
            Assert.Equal("a", input.ReadLine());
            Assert.Equal("b", input.ReadLine());
            Assert.Null(input.ReadLine());
        }
    }
}