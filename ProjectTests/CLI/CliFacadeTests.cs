using System;
using System.Collections.Generic;
using System.Linq;
using GameSystem.Core.Interfaces;
using Infra.Display;
using Moq;
using OOPproject.SOLID;
using ProjectTests.Mocks;
using Xunit;

namespace ProjectTests.CliTests
{
    public class ArgsTests
    {
        [Fact]
        public void Args_KeepsPositionalWordsAndOptions()
        {
            // Act
            var args = new Args(new[] { "a", "b" }, new Dictionary<string, string> { ["--id"] = "5", ["--flag"] = "" });

            // Assert
            Assert.Equal(new[] { "a", "b" }, args.Positional);
            Assert.True(args.Has("--id"));
            Assert.Equal("5", args.Get("--id"));
            Assert.Equal(string.Empty, args.Get("--flag"));
            Assert.Null(args.Get("--missing"));
        }

        [Fact]
        public void Options_AreCaseInsensitive()
        {
            // Act
            var args = new Args(null, new Dictionary<string, string> { ["--ID"] = "5" });

            // Assert
            Assert.True(args.Has("--id"));
        }

        [Fact]
        public void Args_AreCopiedFromTheirSource()
        {
            // Arrange
            var options = new Dictionary<string, string> { ["--id"] = "5" };
            var words = new List<string> { "a" };
            var args = new Args(words, options);

            // Act
            options["--other"] = "x";
            words.Add("b");

            // Assert
            Assert.False(args.Has("--other"));
            Assert.Equal(new[] { "a" }, args.Positional);
        }

        [Fact]
        public void Joined_JoinsTheWordsFromAnIndex()
        {
            // Arrange
            var args = new Args(new[] { "show", "Shi", "Fuu" });

            // Act & Assert
            Assert.Equal("show Shi Fuu", args.Joined());
            Assert.Equal("Shi Fuu", args.Joined(1));
        }

        [Fact]
        public void EmptyArgs_HaveNothing()
        {
            // Act
            var args = new Args();

            // Assert
            Assert.Empty(args.Positional);
            Assert.Empty(args.Options);
        }
    }

    public class ArgsParserTests
    {
        private readonly ArgsParser _parser = new ArgsParser();

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void ParseArgs_EmptyLine_GivesNull(string line)
        {
            // Act & Assert
            Assert.Null(_parser.ParseArgs(line));
        }

        [Fact]
        public void ParseArgs_SplitsSelectorWordsAndOptions()
        {
            // Act
            var parsed = _parser.ParseArgs("cmd a b --id 5 --flag")!;

            // Assert
            Assert.Equal("cmd", parsed.Selector);
            Assert.Equal(new[] { "a", "b" }, parsed.Args.Positional);
            Assert.Equal("5", parsed.Args.Get("--id"));
            Assert.Equal(string.Empty, parsed.Args.Get("--flag"));
        }

        [Fact]
        public void ParseArgs_QuotedTextIsOneWord()
        {
            // Act
            var parsed = _parser.ParseArgs("add heading \"Chapter 1\"")!;

            // Assert
            Assert.Equal(new[] { "heading", "Chapter 1" }, parsed.Args.Positional);
        }

        [Fact]
        public void ParseArgs_OnlyTheSelector_GivesEmptyArgs()
        {
            // Act
            var parsed = _parser.ParseArgs("undo")!;

            // Assert
            Assert.Equal("undo", parsed.Selector);
            Assert.Empty(parsed.Args.Positional);
            Assert.Empty(parsed.Args.Options);
        }

        [Fact]
        public void Tokenize_KeepsAnApostropheInsideAWord()
        {
            // Act
            var tokens = ArgsParser.Tokenize("п'ять 'two words'");

            // Assert
            Assert.Equal(new[] { "п'ять", "two words" }, tokens);
        }
    }

    public class StrategyAndCliTests
    {
        private sealed class TestStrategy : CommandStrategyBase
        {
            public TestStrategy(params string[] selectors)
            {
                foreach (string name in selectors)
                {
                    string selector = name;
                    Register(selector, (args, opts) => Calls.Add(selector + ":" + string.Join(",", args)));
                }
            }

            public List<string> Calls { get; } = new List<string>();
        }

        /// <summary>A strategy written directly against the interface: the Cli must not need anything more.</summary>
        private sealed class CapturingStrategy : ICommandStrategy
        {
            public Args? Received { get; private set; }

            public IReadOnlyCollection<string> GetCommandSelectors() => new[] { "cap" };

            public void RegisterCommand(string selector, ICommand command) { }

            public void ExecCommand(string selector, Args args) => Received = args;
        }

        private sealed class UpperRenderer : IRenderer<string>
        {
            public string Render(string data) => data.ToUpperInvariant();
        }

        private readonly DisplayerMock _displayer = new DisplayerMock();
        private readonly Cli _cli;
        private readonly Presenter _presenter;

        public StrategyAndCliTests()
        {
            _presenter = new Presenter(_displayer, new InputReaderStub());
            _cli = new Cli(_presenter);
        }

        // ---------- the base class of strategies ----------

        [Fact]
        public void Strategy_ListsItsSelectors()
        {
            // Arrange
            var strategy = new TestStrategy("b", "a");

            // Act & Assert
            Assert.Equal(new[] { "a", "b" }, strategy.GetCommandSelectors().OrderBy(s => s));
        }

        [Fact]
        public void Strategy_ExecCommand_RunsTheCommandWithItsArgs()
        {
            // Arrange
            var strategy = new TestStrategy("a");

            // Act
            strategy.ExecCommand("A", new Args(new[] { "x", "y" }));

            // Assert
            Assert.Equal(new[] { "a:x,y" }, strategy.Calls);
        }

        [Fact]
        public void Strategy_ExecCommand_UnknownSelector_Throws()
        {
            // Arrange
            var strategy = new TestStrategy("a");

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => strategy.ExecCommand("zzz", new Args()));
        }

        [Fact]
        public void Strategy_RegisterCommand_RejectsBadInput()
        {
            // Arrange
            var strategy = new TestStrategy();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => strategy.RegisterCommand("x", null!));
            Assert.Throws<ArgumentException>(() => strategy.RegisterCommand("  ", new DelegateCommand((a, o) => { })));
        }

        // ---------- the facade ----------

        [Fact]
        public void ExecCommand_RoutesToTheStrategyThatOwnsTheSelector()
        {
            // Arrange
            var first = new TestStrategy("a");
            var second = new TestStrategy("b");
            _cli.UseStrategy(first);
            _cli.UseStrategy(second);

            // Act
            _cli.ExecCommand("b one");

            // Assert
            Assert.Empty(first.Calls);
            Assert.Equal(new[] { "b:one" }, second.Calls);
        }

        [Fact]
        public void ExecCommand_PassesTheParsedArgsToTheStrategy()
        {
            // Arrange
            var strategy = new CapturingStrategy();
            _cli.UseStrategy(strategy);

            // Act
            _cli.ExecCommand("cap one \"two words\" --id 5");

            // Assert
            Assert.Equal(new[] { "one", "two words" }, strategy.Received!.Positional);
            Assert.Equal("5", strategy.Received.Get("--id"));
        }

        [Fact]
        public void ExecCommand_UnknownCommand_IsReported()
        {
            // Act
            _cli.ExecCommand("foo bar");

            // Assert
            Assert.Equal("Unknown command: 'foo'. Type 'help' for available commands.", Assert.Single(_displayer.Lines));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void ExecCommand_EmptyLine_DoesNothing(string line)
        {
            // Act
            _cli.ExecCommand(line);

            // Assert
            Assert.Empty(_displayer.Lines);
        }

        [Fact]
        public void ExecCommand_ACommandThatFails_ShowsTheError()
        {
            // Arrange
            var strategy = new TestStrategy();
            strategy.RegisterCommand("boom", new DelegateCommand((a, o) => throw new InvalidOperationException("boom!")));
            _cli.UseStrategy(strategy);

            // Act
            _cli.ExecCommand("boom");

            // Assert
            Assert.Equal("[Error executing command]: boom!", Assert.Single(_displayer.Lines));
        }

        [Fact]
        public void UseStrategy_TheLaterStrategyWinsForTheSameSelector()
        {
            // Arrange
            var first = new TestStrategy("a");
            var second = new TestStrategy("a");
            _cli.UseStrategy(first);
            _cli.UseStrategy(second);

            // Act
            _cli.ExecCommand("a");

            // Assert
            Assert.Empty(first.Calls);
            Assert.Single(second.Calls);
        }

        [Fact]
        public void UseStrategy_TheSameStrategyTwice_IsListedOnce()
        {
            // Arrange
            var strategy = new TestStrategy("a");

            // Act
            _cli.UseStrategy(strategy);
            _cli.UseStrategy(strategy);

            // Assert
            Assert.Single(_cli.Strategies);
        }

        [Fact]
        public void UseStrategy_Null_Throws()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _cli.UseStrategy(null!));
        }

        // ---------- display ----------

        [Fact]
        public void Display_ShowsTheRenderableWithItsRenderer()
        {
            // Arrange
            var renderable = new Renderable<string>("abc", new UpperRenderer());

            // Act
            _cli.Display(renderable);

            // Assert
            Assert.Equal("ABC", Assert.Single(_displayer.Lines));
            Assert.Equal(1, _displayer.DisplayCount);
        }

        [Fact]
        public void Display_WithoutARenderer_Throws()
        {
            // Arrange
            var renderable = new Renderable<string>("abc");

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _cli.Display(renderable));
        }

        [Fact]
        public void Display_ShowsAnObjectThatRendersItself()
        {
            // Arrange
            var displayable = new Mock<IDisplayable>();
            displayable.Setup(d => d.Render()).Returns("self");

            // Act
            _cli.Display(displayable.Object);

            // Assert
            Assert.Equal("self", Assert.Single(_displayer.Lines));
        }

        // ---------- the system strategy ----------

        [Fact]
        public void CommandsCommand_ListsTheSelectorsOfEveryStrategy()
        {
            // Arrange
            _cli.UseStrategy(new TestStrategy("zeta", "alpha"));
            _cli.UseStrategy(new SystemStrategy(_cli, _presenter));

            // Act
            _cli.ExecCommand("commands");

            // Assert
            Assert.Equal(
                new[] { "=== TestStrategy ===", "  alpha, zeta", "=== SystemStrategy ===", "  commands" },
                _displayer.Lines);
        }

        // ---------- the existing handlers are strategies ----------

        [Fact]
        public void TheHandlers_AreStrategiesWithTheirOwnSelectors()
        {
            // Arrange
            var logger = Mock.Of<IGameLogger>();
            var saves = new Infra.Data.SaveManager(new InMemoryStorage());
            var characters = new CharactersCliHandler(logger, _presenter, saves);
            var text = new TextCliHandler(new OOPTextProject.TextSystem.TextFactory("Doc"), _presenter, saves);

            // Act & Assert
            Assert.Contains("create", characters.GetCommandSelectors());
            Assert.Contains("act", characters.GetCommandSelectors());
            Assert.Contains("undo", text.GetCommandSelectors());
            Assert.DoesNotContain("undo", characters.GetCommandSelectors());
        }
    }
}