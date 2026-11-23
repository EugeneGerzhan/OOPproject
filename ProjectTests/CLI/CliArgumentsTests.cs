using OOPproject.SOLID;
using Xunit;

namespace ProjectTests.CliTests
{
    public class CliArgumentsTests
    {
        [Fact]
        public void NoArguments_GiveEmptyResult()
        {
            // Act
            var cli = new CliArguments(new string[0]);

            // Assert
            Assert.Empty(cli.Args);
            Assert.Empty(cli.Options);
        }

        [Fact]
        public void PlainWords_AreArguments()
        {
            // Act
            var cli = new CliArguments(new[] { "a", "b" });

            // Assert
            Assert.Equal(new[] { "a", "b" }, cli.Args);
            Assert.Empty(cli.Options);
        }

        [Fact]
        public void OptionWithValue_StoresTheValue()
        {
            // Act
            var cli = new CliArguments(new[] { "--id", "5" });

            // Assert
            Assert.Empty(cli.Args);
            Assert.Equal("5", cli.Options["--id"]);
        }

        [Fact]
        public void MixedArgumentsAndOptions_AreSeparated()
        {
            // Act
            var cli = new CliArguments(new[] { "add", "item", "--char_id", "Bob", "--id", "3" });

            // Assert
            Assert.Equal(new[] { "add", "item" }, cli.Args);
            Assert.Equal("Bob", cli.Options["--char_id"]);
            Assert.Equal("3", cli.Options["--id"]);
        }

        [Fact]
        public void Flag_HasEmptyValue()
        {
            // Act
            var cli = new CliArguments(new[] { "--whole" });

            // Assert
            Assert.True(cli.Options.ContainsKey("--whole"));
            Assert.Equal(string.Empty, cli.Options["--whole"]);
        }

        [Fact]
        public void TwoFlagsInARow_AreBothFlags()
        {
            // Act
            var cli = new CliArguments(new[] { "--whole", "--id" });

            // Assert
            Assert.True(cli.Options.ContainsKey("--whole"));
            Assert.True(cli.Options.ContainsKey("--id"));
            Assert.Empty(cli.Args);
        }

        [Fact]
        public void OptionNames_AreCaseInsensitive()
        {
            // Act
            var cli = new CliArguments(new[] { "--ID", "5" });

            // Assert
            Assert.True(cli.Options.ContainsKey("--id"));
        }

        [Fact]
        public void WordAfterOptionValue_IsAnArgumentAgain()
        {
            // Act
            var cli = new CliArguments(new[] { "--id", "5", "extra" });

            // Assert
            Assert.Equal("5", cli.Options["--id"]);
            Assert.Equal(new[] { "extra" }, cli.Args);
        }
    }
}