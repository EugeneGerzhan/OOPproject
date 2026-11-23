using System;
using Infra.Data;
using Infra.Display;
using ProjectTests.Mocks;
using Xunit;

namespace ProjectTests.RenderTests
{
    public class RenderableTests
    {
        private sealed class UpperRenderer : IRenderer<string>
        {
            public string Render(string data) => data.ToUpperInvariant();
        }

        private sealed class BracketRenderer : IRenderer<string>
        {
            public string Render(string data) => $"[{data}]";
        }

        [Fact]
        public void Render_UsesTheRendererOfTheRenderable()
        {
            // Arrange
            var renderable = new Renderable<string>("abc", new UpperRenderer());

            // Act & Assert
            Assert.Equal("ABC", renderable.Render());
            Assert.Equal("abc", renderable.Data);
            Assert.True(renderable.HasRenderer);
        }

        [Fact]
        public void UseRenderer_ReplacesTheRenderer()
        {
            // Arrange
            var renderable = new Renderable<string>("abc", new UpperRenderer());

            // Act
            renderable.UseRenderer(new BracketRenderer());

            // Assert
            Assert.Equal("[abc]", renderable.Render());
        }

        [Fact]
        public void Render_WithoutRenderer_Throws()
        {
            // Arrange
            var renderable = new Renderable<string>("abc");

            // Act & Assert
            Assert.False(renderable.HasRenderer);
            Assert.Throws<InvalidOperationException>(() => renderable.Render());
        }

        [Fact]
        public void RenderWithAnotherRenderer_DoesNotChangeTheOwnOne()
        {
            // Arrange
            var renderable = new Renderable<string>("abc", new UpperRenderer());

            // Act
            string other = renderable.Render(new BracketRenderer());

            // Assert
            Assert.Equal("[abc]", other);
            Assert.Equal("ABC", renderable.Render());
        }

        [Fact]
        public void RenderWithAnotherRenderer_WorksWithoutAnOwnOne()
        {
            // Arrange
            var renderable = new Renderable<string>("abc");

            // Act & Assert
            Assert.Equal("[abc]", renderable.Render(new BracketRenderer()));
        }

        [Fact]
        public void UseRenderer_Null_Throws()
        {
            // Arrange
            var renderable = new Renderable<string>("abc");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => renderable.UseRenderer(null!));
        }

        [Fact]
        public void ARenderable_IsDisplayable()
        {
            // Arrange
            var displayer = new DisplayerMock();
            IDisplayable displayable = new Renderable<string>("abc", new UpperRenderer());

            // Act
            displayer.Display(displayable);

            // Assert
            Assert.Equal("ABC", Assert.Single(displayer.Lines));
        }
    }

    public class BattleResultsDataTests
    {
        private static BattleResultData Result(string? winner, int rounds) =>
            new BattleResultData(winner, new System.Collections.Generic.List<string> { "A", "B" }, rounds, rounds * 2, new DateTime(2026, 1, 2, 3, 4, 0));

        [Fact]
        public void AddBattleResult_AppendsAndPersists()
        {
            // Arrange
            var storage = new InMemoryStorage();
            var manager = new SaveManager(storage);

            // Act
            manager.AddBattleResult(Result("A", 2));
            manager.AddBattleResult(Result(null, 3));

            // Assert
            var results = manager.ListBattleResults();
            Assert.Equal(2, results.Count);
            Assert.Equal("A", results[0].Winner);
            Assert.Null(results[1].Winner);
            Assert.Equal(2, storage.SaveCalls);
        }

        [Fact]
        public void ListBattleResults_WhenNothingWasSaved_IsEmpty()
        {
            // Arrange
            var manager = new SaveManager(new InMemoryStorage());

            // Act & Assert
            Assert.Empty(manager.ListBattleResults());
        }

        [Fact]
        public void Results_SurviveTheJsonFile()
        {
            // Arrange
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"oop-results-{Guid.NewGuid():N}.json");
            try
            {
                var manager = new SaveManager(new JsonRepositoryStorage(path));
                manager.AddBattleResult(Result("A", 2));
                manager.AddBattleResult(Result(null, 1));

                // Act
                var loaded = new SaveManager(new JsonRepositoryStorage(path)).ListBattleResults();

                // Assert
                Assert.Equal(2, loaded.Count);
                Assert.Equal("A", loaded[0].Winner);
                Assert.Equal(new[] { "A", "B" }, loaded[0].Participants);
                Assert.Equal(2, loaded[0].Rounds);
                Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 0), loaded[0].FinishedAt);
                Assert.Null(loaded[1].Winner);
            }
            finally
            {
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }

        [Fact]
        public void AnOldFileWithoutResults_StillLoads()
        {
            // Arrange
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"oop-old-{Guid.NewGuid():N}.json");
            try
            {
                System.IO.File.WriteAllText(path, "{ \"CharacterSaves\": {}, \"TextSaves\": {} }");

                // Act
                var results = new SaveManager(new JsonRepositoryStorage(path)).ListBattleResults();

                // Assert
                Assert.Empty(results);
            }
            finally
            {
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }
    }
}