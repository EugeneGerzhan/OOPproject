using System;
using System.Collections.Generic;
using OOPproject.SOLID;
using OOPTextProject.TextSystem;
using ProjectTests.Mocks;
using Xunit;

namespace ProjectTests.CliTests
{
    public class CommandObjectsTests
    {
        private sealed class RecordingCommand : ICommand
        {
            public int Calls { get; private set; }
            public string[]? Args { get; private set; }
            public Dictionary<string, string>? Options { get; private set; }

            public void Execute(string[] args, Dictionary<string, string> options)
            {
                Calls++;
                Args = args;
                Options = options;
            }
        }

        private static CommandInterpreter NewInterpreter() =>
            new CommandInterpreter(new Presenter(new DisplayerMock(), new InputReaderStub()));

        [Fact]
        public void Interpreter_RunsACommandObject()
        {
            // Arrange
            var interpreter = NewInterpreter();
            var command = new RecordingCommand();
            interpreter.RegisterCommand("cmd", command);

            // Act
            interpreter.Execute("cmd one two --flag value");

            // Assert
            Assert.Equal(1, command.Calls);
            Assert.Equal(new[] { "one", "two" }, command.Args!);
            Assert.Equal("value", command.Options!["--flag"]);
        }

        [Fact]
        public void Interpreter_AcceptsBothCommandObjectsAndMethods()
        {
            // Arrange
            var interpreter = NewInterpreter();
            var command = new RecordingCommand();
            int calls = 0;
            interpreter.RegisterCommand("object", command);
            interpreter.RegisterCommand("method", (args, opts) => calls++);

            // Act
            interpreter.Execute("object");
            interpreter.Execute("method");

            // Assert
            Assert.Equal(1, command.Calls);
            Assert.Equal(1, calls);
        }

        [Fact]
        public void RegisteringAnotherCommandUnderTheSameName_ReplacesIt()
        {
            // Arrange
            var interpreter = NewInterpreter();
            var first = new RecordingCommand();
            var second = new RecordingCommand();
            interpreter.RegisterCommand("cmd", first);
            interpreter.RegisterCommand("cmd", second);

            // Act
            interpreter.Execute("cmd");

            // Assert
            Assert.Equal(0, first.Calls);
            Assert.Equal(1, second.Calls);
        }

        [Fact]
        public void DelegateCommand_ForwardsToTheAction()
        {
            // Arrange
            string[]? receivedArgs = null;
            var command = new DelegateCommand((args, opts) => receivedArgs = args);

            // Act
            command.Execute(new[] { "a" }, new Dictionary<string, string>());

            // Assert
            Assert.Equal(new[] { "a" }, receivedArgs!);
        }

        [Fact]
        public void DelegateCommand_NullAction_Throws()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DelegateCommand(null!));
        }
    }

    public class UndoableCommandTests
    {
        private static readonly string[] NoArgs = Array.Empty<string>();

        [Fact]
        public void ACommandThatChangesTheDocument_IsRemembered()
        {
            // Arrange
            var factory = new TextFactory("Doc");
            var history = new UndoHistory();
            var command = new UndoableCommand(new DelegateCommand((a, o) => factory.AddHeading("A")), factory, history);

            // Act
            command.Execute(NoArgs, new Dictionary<string, string>());

            // Assert
            Assert.Equal(1, history.Count);
        }

        [Fact]
        public void ACommandThatChangesNothing_IsNotRemembered()
        {
            // Arrange
            var factory = new TextFactory("Doc");
            var history = new UndoHistory();
            var command = new UndoableCommand(new DelegateCommand((a, o) => factory.Up()), factory, history);

            // Act
            command.Execute(NoArgs, new Dictionary<string, string>());

            // Assert
            Assert.Equal(0, history.Count);
        }

        [Fact]
        public void ACommandThatFails_IsNotRememberedAndTheErrorReachesTheCaller()
        {
            // Arrange
            var factory = new TextFactory("Doc");
            var history = new UndoHistory();
            var command = new UndoableCommand(
                new DelegateCommand((a, o) => throw new InvalidOperationException("boom")), factory, history);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => command.Execute(NoArgs, new Dictionary<string, string>()));
            Assert.Equal(0, history.Count);
        }

        [Fact]
        public void TheRememberedSnapshot_IsTheStateBeforeTheCommand()
        {
            // Arrange
            var factory = new TextFactory("Doc");
            var history = new UndoHistory();
            var command = new UndoableCommand(new DelegateCommand((a, o) => factory.AddHeading("A")), factory, history);
            command.Execute(NoArgs, new Dictionary<string, string>());

            // Act
            history.TryPop(out var memento);
            factory.Restore(memento!);

            // Assert
            Assert.Empty(factory.Document.Children);
        }
    }

    public class UndoCliTests
    {
        private readonly TextCliFixture _f = new TextCliFixture();

        [Fact]
        public void Undo_WithEmptyHistory_SaysNothingToUndo()
        {
            // Act
            var lines = _f.Run("undo");

            // Assert
            Assert.Equal("Nothing to undo.", Assert.Single(lines));
        }

        [Fact]
        public void Undo_RevertsTheLastChange()
        {
            // Arrange
            _f.Run("add heading \"A\"");
            _f.Run("add leaf paragraph \"p\"");

            // Act
            var lines = _f.Run("undo");

            // Assert
            Assert.Equal("Undone. Current path: /A (1 more step(s) can be undone).", Assert.Single(lines));
            var heading = Assert.IsType<Heading>(Assert.Single(_f.Factory.Document.Children));
            Assert.Empty(heading.Children);
        }

        [Fact]
        public void Undo_CanBeRepeatedUntilTheHistoryIsEmpty()
        {
            // Arrange
            _f.Run("add heading \"A\"");
            _f.Run("add leaf paragraph \"p\"");
            _f.Run("undo");

            // Act
            var second = _f.Run("undo");
            var third = _f.Run("undo");

            // Assert
            Assert.Equal("Undone. Current path: / (0 more step(s) can be undone).", Assert.Single(second));
            Assert.Equal("Nothing to undo.", Assert.Single(third));
            Assert.Empty(_f.Factory.Document.Children);
        }

        [Fact]
        public void Undo_RestoresARemovedElement()
        {
            // Arrange
            _f.Run("add heading \"A\"");
            _f.Run("up");
            _f.Run("rm A", "y");
            Assert.Empty(_f.Factory.Document.Children);

            // Act
            var lines = _f.Run("undo");

            // Assert
            Assert.Equal("Undone. Current path: / (1 more step(s) can be undone).", Assert.Single(lines));
            Assert.Single(_f.Factory.Document.Children);
        }

        [Fact]
        public void Undo_RemovesACopy()
        {
            // Arrange
            _f.Factory.AddHeading("Chapter");
            _f.Factory.AddParagraph("text");
            _f.Factory.Up();
            _f.Run("cp Chapter");
            Assert.Equal(2, _f.Factory.Document.Children.Count);

            // Act
            _f.Run("undo");

            // Assert
            Assert.Single(_f.Factory.Document.Children);
        }

        [Fact]
        public void Undo_RestoresTheDocumentThatWasReplacedByLoad()
        {
            // Arrange
            _f.Factory.AddHeading("Saved");
            _f.Factory.Up();
            _f.Run("save demo");
            _f.Factory.AddHeading("Extra");
            _f.Factory.Up();
            _f.Run("load demo", "y");
            Assert.Single(_f.Factory.Document.Children);

            // Act
            _f.Run("undo");

            // Assert
            Assert.Equal(2, _f.Factory.Document.Children.Count);
        }

        [Fact]
        public void CommandsThatChangeNothing_AreNotInTheHistory()
        {
            // Arrange
            _f.Run("add");
            _f.Run("rm Missing");
            _f.Run("cp Missing");
            _f.Run("load nope");
            _f.Run("print");
            _f.Run("pwd");

            // Act
            var lines = _f.Run("undo");

            // Assert
            Assert.Equal("Nothing to undo.", Assert.Single(lines));
        }

        [Fact]
        public void ADeclinedRemoval_IsNotInTheHistory()
        {
            // Arrange
            _f.Factory.AddHeading("A");
            _f.Factory.Up();
            _f.Run("rm A", "n");

            // Act
            var lines = _f.Run("undo");

            // Assert
            Assert.Equal("Nothing to undo.", Assert.Single(lines));
            Assert.Single(_f.Factory.Document.Children);
        }

        [Fact]
        public void Navigation_IsNotAStepOfTheHistory()
        {
            // Arrange
            _f.Run("add heading \"A\"");
            _f.Run("up");
            _f.Run("cd /A");
            _f.Run("up");

            // Act
            var lines = _f.Run("undo");

            // Assert
            Assert.Equal("Undone. Current path: / (0 more step(s) can be undone).", Assert.Single(lines));
            Assert.Empty(_f.Factory.Document.Children);
        }

        [Fact]
        public void Help_MentionsUndo()
        {
            // Act
            var lines = _f.Run("help");

            // Assert
            Assert.Contains(lines, l => l.TrimStart().StartsWith("undo"));
        }
    }
}