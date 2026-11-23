using System;
using System.Net.Http;
using System.Threading.Tasks;
using Infra.WebApi;
using ProjectTests.Mocks;
using Xunit;

namespace ProjectTests.WebApiTests
{
    public class ImageLinkTests
    {
        private static GenshinApiClient CreateClient(StubHttpMessageHandler handler) =>
            new GenshinApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://test.local/") });

        [Fact]
        public void Parser_ReadsTheImageLink()
        {
            // Act
            var character = GenshinJsonParser.ParseCharacter("x", "{ \"name\": \"X\", \"image\": \"https://example.com/x.png\" }");

            // Assert
            Assert.Equal("https://example.com/x.png", character.ImageUrl);
        }

        [Fact]
        public void Parser_WithoutAnImage_GivesAnEmptyLink()
        {
            // Act
            var character = GenshinJsonParser.ParseCharacter("x", "{ \"name\": \"X\" }");

            // Assert
            Assert.Equal(string.Empty, character.ImageUrl);
        }

        [Fact]
        public async Task Client_KeepsTheLinkOfTheAnswer()
        {
            // Arrange
            var client = CreateClient(StubHttpMessageHandler.Json("{ \"name\": \"Albedo\", \"image\": \"https://example.com/a.png\" }"));

            // Act
            var character = await client.GetCharacterAsync("albedo");

            // Assert
            Assert.Equal("https://example.com/a.png", character.ImageUrl);
        }

        [Fact]
        public async Task Client_BuildsTheLinkFromTheEndpointWhenTheAnswerHasNone()
        {
            // Arrange
            var client = CreateClient(StubHttpMessageHandler.Json("{ \"name\": \"Albedo\" }"));

            // Act
            var character = await client.GetCharacterAsync("Albedo");

            // Assert
            Assert.Equal("https://test.local/characters/albedo/card", character.ImageUrl);
        }

        [Fact]
        public async Task Offline_GivesALinkToo()
        {
            // Act
            var character = await new OfflineGameDataApi().GetCharacterAsync("albedo");

            // Assert
            Assert.Equal("https://genshin.jmp.blue/characters/albedo/card", character.ImageUrl);
        }
    }
}