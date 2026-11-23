using Infra.Display;
using Moq;
using OOPproject.SOLID;
using ProjectTests.Mocks;
using Xunit;

namespace ProjectTests.CliTests
{
    public class PresenterTests
    {
        [Fact]
        public void Show_String_GoesToDisplayer()
        {
            // Arrange
            var displayer = new Mock<IDisplayer>();
            var presenter = new Presenter(displayer.Object, Mock.Of<IInputReader>());

            // Act
            presenter.Show("hello");

            // Assert
            displayer.Verify(d => d.Display("hello"), Times.Once());
        }

        [Fact]
        public void Show_Displayable_GoesToDisplayer()
        {
            // Arrange
            var displayer = new Mock<IDisplayer>();
            var displayable = new Mock<IDisplayable>();
            var presenter = new Presenter(displayer.Object, Mock.Of<IInputReader>());

            // Act
            presenter.Show(displayable.Object);

            // Assert
            displayer.Verify(d => d.Display(displayable.Object), Times.Once());
        }

        [Fact]
        public void Prompt_WritesPromptAndReturnsTrimmedAnswer()
        {
            // Arrange
            var displayer = new Mock<IDisplayer>();
            var input = new Mock<IInputReader>();
            input.Setup(i => i.ReadLine()).Returns("  Bob  ");
            var presenter = new Presenter(displayer.Object, input.Object);

            // Act
            string answer = presenter.Prompt("Name: ");

            // Assert
            Assert.Equal("Bob", answer);
            displayer.Verify(d => d.Write("Name: "), Times.Once());
        }

        [Fact]
        public void Prompt_EndOfInput_ReturnsEmptyString()
        {
            // Arrange
            var presenter = new Presenter(new DisplayerFake(), new InputReaderStub());

            // Act
            string answer = presenter.Prompt("Name: ");

            // Assert
            Assert.Equal(string.Empty, answer);
        }

        [Theory]
        [InlineData("y", true)]
        [InlineData("Y", true)]
        [InlineData("yes", true)]
        [InlineData("YES", true)]
        [InlineData(" y ", true)]
        [InlineData("n", false)]
        [InlineData("", false)]
        [InlineData("maybe", false)]
        public void Confirm_IsTrueOnlyForYes(string answer, bool expected)
        {
            // Arrange
            var presenter = new Presenter(new DisplayerFake(), new InputReaderStub(answer));

            // Act
            bool result = presenter.Confirm("Sure? ");

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void PromptInt_EmptyAnswer_UsesDefault()
        {
            // Arrange
            var displayer = new DisplayerMock();
            var presenter = new Presenter(displayer, new InputReaderStub(""));

            // Act
            int value = presenter.PromptInt("Count", 5, 0);

            // Assert
            Assert.Equal(5, value);
            Assert.Equal("Count [5]: ", Assert.Single(displayer.Writes));
        }

        [Fact]
        public void PromptInt_ValidNumber_IsReturned()
        {
            // Arrange
            var presenter = new Presenter(new DisplayerFake(), new InputReaderStub("42"));

            // Act & Assert
            Assert.Equal(42, presenter.PromptInt("Count", 5, 0));
        }

        [Fact]
        public void PromptInt_InvalidAnswers_AreRepeatedUntilValid()
        {
            // Arrange
            var displayer = new DisplayerMock();
            var presenter = new Presenter(displayer, new InputReaderStub("abc", "-1", "7"));

            // Act
            int value = presenter.PromptInt("Count", 5, 0);

            // Assert
            Assert.Equal(7, value);
            Assert.Equal(2, displayer.DisplayLineCount);
            Assert.All(displayer.Lines, line => Assert.Equal("Enter an integer >= 0.", line));
        }

        [Fact]
        public void PromptChoice_EmptyAnswer_ReturnsFirstOption()
        {
            // Arrange
            var displayer = new DisplayerMock();
            var presenter = new Presenter(displayer, new InputReaderStub(""));

            // Act
            string choice = presenter.PromptChoice("Class", new[] { "barbarian", "monk" });

            // Assert
            Assert.Equal("barbarian", choice);
            Assert.Equal("Class (barbarian/monk) [barbarian]: ", Assert.Single(displayer.Writes));
        }

        [Fact]
        public void PromptChoice_IsCaseInsensitive_AndReturnsLowercase()
        {
            // Arrange
            var presenter = new Presenter(new DisplayerFake(), new InputReaderStub("MONK"));

            // Act & Assert
            Assert.Equal("monk", presenter.PromptChoice("Class", new[] { "barbarian", "monk" }));
        }

        [Fact]
        public void PromptChoice_InvalidAnswer_IsRepeated()
        {
            // Arrange
            var displayer = new DisplayerMock();
            var presenter = new Presenter(displayer, new InputReaderStub("wizard", "monk"));

            // Act
            string choice = presenter.PromptChoice("Class", new[] { "barbarian", "monk" });

            // Assert
            Assert.Equal("monk", choice);
            Assert.Equal("Choose one of: barbarian, monk.", Assert.Single(displayer.Lines));
        }
    }
}
