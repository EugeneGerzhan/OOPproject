using System;
using System.Collections.Generic;
using Infra.Display;
using Moq;
using OOPproject.SOLID;
using ProjectTests.Mocks;
using Xunit;

namespace ProjectTests.CliTests
{
    public class CommandInterpreterTests
    {
        private readonly DisplayerMock _displayer = new DisplayerMock();
        private readonly CommandInterpreter _interpreter;

        public CommandInterpreterTests()
        {
            _interpreter = new CommandInterpreter(new Presenter(_displayer, new InputReaderStub()));
        }

        private (string[] args, Dictionary<string, string> opts) Capture(string input)
        {
            string[] args = Array.Empty<string>();
            var opts = new Dictionary<string, string>();
            _interpreter.RegisterCommand("cmd", (a, o) =>
            {
                args = a;
                opts = o;
            });

            _interpreter.Execute(input);
            return (args, opts);
        }

        [Fact]
        public void Execute_CallsRegisteredCommandWithArgumentsAndOptions()
        {
            // Act
            var (args, opts) = Capture("cmd one two --flag value");

            // Assert
            Assert.Equal(new[] { "one", "two" }, args);
            Assert.Equal("value", opts["--flag"]);
        }

        [Fact]
        public void Execute_CommandNameIsCaseInsensitive()
        {
            // Arrange
            int calls = 0;
            _interpreter.RegisterCommand("cmd", (a, o) => calls++);

            // Act
            _interpreter.Execute("CMD");

            // Assert
            Assert.Equal(1, calls);
        }

        [Fact]
        public void RegisterCommand_SameNameTwice_ReplacesHandler()
        {
            // Arrange
            int first = 0, second = 0;
            _interpreter.RegisterCommand("cmd", (a, o) => first++);
            _interpreter.RegisterCommand("cmd", (a, o) => second++);

            // Act
            _interpreter.Execute("cmd");

            // Assert
            Assert.Equal(0, first);
            Assert.Equal(1, second);
        }

        [Fact]
        public void Execute_UnknownCommand_ReportsIt()
        {
            // Act
            _interpreter.Execute("foo bar");

            // Assert
            Assert.Equal("Unknown command: 'foo'. Type 'help' for available commands.", Assert.Single(_displayer.Lines));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Execute_EmptyInput_DoesNothing(string input)
        {
            // Act
            _interpreter.Execute(input);

            // Assert
            Assert.Empty(_displayer.Lines);
        }

        [Fact]
        public void Execute_HandlerThrows_ErrorIsReported()
        {
            // Arrange
            _interpreter.RegisterCommand("boom", (a, o) => throw new InvalidOperationException("boom!"));

            // Act
            _interpreter.Execute("boom");

            // Assert
            Assert.Equal("[Error executing command]: boom!", Assert.Single(_displayer.Lines));
        }

        [Fact]
        public void Execute_HandlerFailsWhileDisplaying_ErrorIsReportedInsteadOfThrown()
        {
            // Arrange
            var stub = new DisplayerStub("bad line", Mock.Of<IDisplayable>());
            var ui = new Presenter(stub, new InputReaderStub());
            var interpreter = new CommandInterpreter(ui);
            interpreter.RegisterCommand("cmd", (a, o) => ui.Show("bad line"));

            // Act
            var exception = Record.Exception(() => interpreter.Execute("cmd"));

            // Assert
            Assert.Null(exception);
        }

        [Theory]
        [InlineData("cmd \"hello world\"", "hello world")]
        [InlineData("cmd 'hello world'", "hello world")]
        [InlineData("cmd \u201Chello world\u201D", "hello world")]
        [InlineData("cmd \u00ABhello world\u00BB", "hello world")]
        [InlineData("cmd it's", "it's")]
        [InlineData("cmd \"abc def", "abc def")]
        public void Execute_QuotedTextIsOneArgument(string input, string expected)
        {
            // Act
            var (args, _) = Capture(input);

            // Assert
            Assert.Equal(new[] { expected }, args);
        }

        [Fact]
        public void Execute_ApostropheInsideUkrainianWord_IsKept()
        {
            // Act
            var (args, _) = Capture("cmd п'ять");

            // Assert
            Assert.Equal(new[] { "п'ять" }, args);
        }

        [Fact]
        public void Execute_EmptyQuotes_GiveEmptyArgument()
        {
            // Act
            var (args, _) = Capture("cmd \"\"");

            // Assert
            Assert.Equal(new[] { "" }, args);
        }

        [Fact]
        public void Execute_QuotedOptionValue_KeepsSpaces()
        {
            // Act
            var (args, opts) = Capture("cmd --name \"John Smith\"");

            // Assert
            Assert.Empty(args);
            Assert.Equal("John Smith", opts["--name"]);
        }
    }
}