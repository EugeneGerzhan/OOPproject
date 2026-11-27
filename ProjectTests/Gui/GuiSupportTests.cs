using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameSystem.Core.Remote;
using Infra.Display;
using Moq;
using OOPproject.SOLID;
using ProjectTests.Mocks;
using Xunit;

namespace ProjectTests.GuiTests
{
    public class NotifyingDisplayerTests
    {
        [Fact]
        public void Display_RaisesTheLineWithALineBreak()
        {
            // Arrange
            var displayer = new NotifyingDisplayer();
            var received = new List<string>();
            displayer.TextWritten += received.Add;

            // Act
            displayer.Display("hello");

            // Assert
            Assert.Equal(new[] { "hello" + Environment.NewLine }, received);
        }

        [Fact]
        public void Write_RaisesTheTextAsItIs_AndRemembersItAsThePrompt()
        {
            // Arrange
            var displayer = new NotifyingDisplayer();
            var received = new List<string>();
            displayer.TextWritten += received.Add;

            // Act
            displayer.Write("Name: ");

            // Assert
            Assert.Equal(new[] { "Name: " }, received);
            Assert.Equal("Name: ", displayer.PendingPrompt);
        }

        [Fact]
        public void Display_ForgetsThePendingPrompt()
        {
            // Arrange
            var displayer = new NotifyingDisplayer();
            displayer.Write("Name: ");

            // Act
            displayer.Display("Albedo"); // the echo of the answer

            // Assert
            Assert.Equal(string.Empty, displayer.PendingPrompt);
        }

        [Fact]
        public void Display_Displayable_ShowsItsRenderedText()
        {
            // Arrange
            var displayer = new NotifyingDisplayer();
            var received = new List<string>();
            displayer.TextWritten += received.Add;
            var displayable = new Mock<IDisplayable>();
            displayable.Setup(d => d.Render()).Returns("rendered");

            // Act
            displayer.Display(displayable.Object);

            // Assert
            Assert.Equal(new[] { "rendered" + Environment.NewLine }, received);
        }

        [Fact]
        public void WithoutSubscribers_NothingFails()
        {
            // Arrange
            var displayer = new NotifyingDisplayer();

            // Act
            displayer.Display("x");
            displayer.Write("y");

            // Assert
            Assert.Equal("y", displayer.PendingPrompt);
        }
    }

    public class PromptTextTests
    {
        [Theory]
        [InlineData("The current state will be replaced. Continue? (y/n): ")]
        [InlineData("Sure? (Y/N):")]
        [InlineData("Sure? (y/n)")]
        public void IsConfirmation_RecognizesYesNoQuestions(string prompt)
        {
            // Act & Assert
            Assert.True(PromptText.IsConfirmation(prompt));
        }

        [Theory]
        [InlineData("")]
        [InlineData("Name: ")]
        [InlineData("Class (barbarian/monk) [barbarian]: ")]
        [InlineData("Is (y/n) a question? yes")]
        public void IsConfirmation_RejectsOtherPrompts(string prompt)
        {
            // Act & Assert
            Assert.False(PromptText.IsConfirmation(prompt));
        }

        [Fact]
        public void Question_RemovesTheYesNoEnding()
        {
            // Act
            string question = PromptText.Question("The current state will be replaced. Continue? (y/n): ");

            // Assert
            Assert.Equal("The current state will be replaced. Continue?", question);
        }

        [Fact]
        public void Question_OfAnotherPrompt_IsTheTrimmedPrompt()
        {
            // Act & Assert
            Assert.Equal("Name:", PromptText.Question("Name: "));
        }
    }

    public class CommandRunnerTests
    {
        private sealed class OneCommand : CommandStrategyBase
        {
            public OneCommand(string selector, Action<string[], Dictionary<string, string>> action)
            {
                Register(selector, action);
            }
        }

        private readonly DisplayerMock _displayer = new DisplayerMock();
        private readonly Cli _cli;

        public CommandRunnerTests()
        {
            var ui = new Presenter(_displayer, new InputReaderStub());
            _cli = new Cli(ui);
            _cli.UseStrategy(new OneCommand("ping", (args, opts) => ui.Show("pong")));
        }

        [Fact]
        public async Task RunAsync_RunsTheCommandLine()
        {
            // Arrange
            var runner = new CommandRunner(_cli);

            // Act
            await runner.RunAsync("ping");

            // Assert
            Assert.Equal("pong", Assert.Single(_displayer.Lines));
        }

        [Fact]
        public async Task RunAsync_RunsAnAction()
        {
            // Arrange
            var runner = new CommandRunner(_cli);
            bool done = false;

            // Act
            await runner.RunAsync(() => done = true);

            // Assert
            Assert.True(done);
        }

        [Fact]
        public async Task TheWorkIsDoneOneAtATime()
        {
            // Arrange
            var runner = new CommandRunner(_cli);
            int active = 0;
            int maxActive = 0;
            var gate = new object();

            void Work()
            {
                int now = Interlocked.Increment(ref active);
                lock (gate)
                {
                    maxActive = Math.Max(maxActive, now);
                }
                Thread.Sleep(30);
                Interlocked.Decrement(ref active);
            }

            // Act
            await Task.WhenAll(runner.RunAsync(Work), runner.RunAsync(Work), runner.RunAsync(Work));

            // Assert
            Assert.Equal(1, maxActive);
        }

        [Fact]
        public async Task AFailingAction_ThrowsAndDoesNotBlockTheNextOne()
        {
            // Arrange
            var runner = new CommandRunner(_cli);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(() => throw new InvalidOperationException("boom")));

            await runner.RunAsync("ping");
            Assert.Equal("pong", Assert.Single(_displayer.Lines));
        }

        [Fact]
        public void Constructor_AndRunAsync_RejectNulls()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new CommandRunner(null!));
            Assert.ThrowsAsync<ArgumentNullException>(() => new CommandRunner(_cli).RunAsync((Action)null!));
        }
    }

    public class CardPresentationTests
    {
        private static CharacterCard Card(string name = "Albedo", int rarity = 5, string vision = "Geo",
                                          string weapon = "Sword", string nation = "Mondstadt",
                                          string description = "A chalk prince.") =>
            new CharacterCard("albedo", name, "Kreideprinz", vision, weapon, rarity, nation, "Knights", description, "",
                new List<TalentCard>(), new List<TalentCard>());

        [Fact]
        public void From_BuildsTheTextsOfTheCard()
        {
            // Act
            var look = CardPresentation.From(Card());

            // Assert
            Assert.Equal("Albedo", look.Name);
            Assert.Equal("Kreideprinz", look.Title);
            Assert.Equal("\u2605\u2605\u2605\u2605\u2605", look.Stars);
            Assert.Equal("Geo \u00B7 Sword \u00B7 Mondstadt", look.Info);
            Assert.Equal("A chalk prince.", look.ShortDescription);
            Assert.Equal("A", look.Initial);
        }

        [Theory]
        [InlineData(5, 5)]
        [InlineData(4, 4)]
        [InlineData(0, 0)]
        [InlineData(9, 5)]
        [InlineData(-3, 0)]
        public void Stars_FollowTheRarityWithinZeroAndFive(int rarity, int expected)
        {
            // Act
            var look = CardPresentation.From(Card(rarity: rarity));

            // Assert
            Assert.Equal(expected, look.Stars.Length);
        }

        [Fact]
        public void Info_SkipsEmptyParts()
        {
            // Act
            var look = CardPresentation.From(Card(nation: " ", weapon: ""));

            // Assert
            Assert.Equal("Geo", look.Info);
        }

        [Theory]
        [InlineData("Anemo", "#4FC3A1")]
        [InlineData("geo", "#D9A441")]
        [InlineData("Electro", "#A96BE0")]
        [InlineData("Dendro", "#7CB342")]
        [InlineData("Hydro", "#3F8EF0")]
        [InlineData("Pyro", "#E8603C")]
        [InlineData("Cryo", "#6FCBE0")]
        [InlineData("Unknown", CardPresentation.DefaultColor)]
        [InlineData("", CardPresentation.DefaultColor)]
        public void TheAccentColor_DependsOnTheVision(string vision, string expected)
        {
            // Act
            var look = CardPresentation.From(Card(vision: vision));

            // Assert
            Assert.Equal(expected, look.AccentColor);
        }

        [Fact]
        public void Initial_OfAnEmptyName_IsAQuestionMark()
        {
            // Act
            var look = CardPresentation.From(Card(name: "  "));

            // Assert
            Assert.Equal("?", look.Initial);
        }

        [Fact]
        public void Initial_IsUppercase()
        {
            // Act
            var look = CardPresentation.From(Card(name: "amber"));

            // Assert
            Assert.Equal("A", look.Initial);
        }

        [Fact]
        public void Shorten_CutsALongTextAndAddsAnEllipsis()
        {
            // Arrange
            string text = new string('a', 300);

            // Act
            string shortText = CardPresentation.Shorten(text, 140);

            // Assert
            Assert.Equal(new string('a', 140) + "\u2026", shortText);
        }

        [Fact]
        public void Shorten_MakesOneLineOutOfManyWhitespaces()
        {
            // Act
            string line = CardPresentation.Shorten("  first\r\n\r\n second \t third  ", 140);

            // Assert
            Assert.Equal("first second third", line);
        }

        [Fact]
        public void Shorten_Null_IsEmpty()
        {
            // Act & Assert
            Assert.Equal(string.Empty, CardPresentation.Shorten(null, 10));
        }

        [Fact]
        public void From_Null_Throws()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => CardPresentation.From(null!));
        }

        [Fact]
        public void TheShortDescription_UsesTheDefaultLength()
        {
            // Act
            var look = CardPresentation.From(Card(description: new string('b', 500)));

            // Assert
            Assert.Equal(CardPresentation.ShortDescriptionLength + 1, look.ShortDescription.Length);
        }
    }
}