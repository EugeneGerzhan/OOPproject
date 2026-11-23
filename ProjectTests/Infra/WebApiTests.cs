using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Infra.WebApi;
using ProjectTests.Mocks;
using Xunit;

namespace ProjectTests.WebApiTests
{
    public class GenshinJsonParserTests : IClassFixture<RemoteSamplesFixture>
    {
        private readonly RemoteSamplesFixture _samples;

        public GenshinJsonParserTests(RemoteSamplesFixture samples)
        {
            _samples = samples;
        }

        [Fact]
        public void ParseCharacter_ReadsProfileFields()
        {
            // Act
            var character = GenshinJsonParser.ParseCharacter("albedo", _samples.AlbedoJson);

            // Assert
            Assert.Equal("albedo", character.Id);
            Assert.Equal("Albedo", character.Name);
            Assert.Equal("Kreideprinz", character.Title);
            Assert.Equal("Geo", character.Vision);
            Assert.Equal("Sword", character.WeaponType);
            Assert.Equal("Mondstadt", character.Nation);
            Assert.Equal("Knights of Favonius", character.Affiliation);
            Assert.Equal(5, character.Rarity);
            Assert.Equal("Chief Alchemist of the Knights of Favonius.", character.Description);
        }

        [Fact]
        public void ParseCharacter_ReadsTalents()
        {
            // Act
            var character = GenshinJsonParser.ParseCharacter("albedo", _samples.AlbedoJson);

            // Assert
            Assert.Equal(3, character.SkillTalents.Count);
            Assert.Equal("Normal Attack", character.SkillTalents[0].Unlock);
            Assert.Equal("Rite of Progeniture: Tectonic Tide", character.SkillTalents[2].Name);
            Assert.Single(character.PassiveTalents);
        }

        [Fact]
        public void ParseCharacter_OnlyNameIsRequired()
        {
            // Act
            var character = GenshinJsonParser.ParseCharacter("x", "{ \"name\": \"X\" }");

            // Assert
            Assert.Equal("X", character.Name);
            Assert.Equal(string.Empty, character.Title);
            Assert.Equal(0, character.Rarity);
            Assert.Empty(character.SkillTalents);
            Assert.Empty(character.PassiveTalents);
        }

        [Fact]
        public void ParseCharacter_RarityGivenAsString_IsParsed()
        {
            // Act
            var character = GenshinJsonParser.ParseCharacter("x", "{ \"name\": \"X\", \"rarity\": \"4\" }");

            // Assert
            Assert.Equal(4, character.Rarity);
        }

        [Fact]
        public void ParseCharacter_NullText_BecomesEmptyString()
        {
            // Act
            var character = GenshinJsonParser.ParseCharacter("x", "{ \"name\": \"X\", \"description\": null }");

            // Assert
            Assert.Equal(string.Empty, character.Description);
        }

        [Fact]
        public void ParseCharacter_SkipsBrokenTalents()
        {
            // Arrange
            string json = "{ \"name\": \"X\", \"skillTalents\": [ { \"name\": \"\" }, 5, { \"name\": \"A\", \"unlock\": \"Elemental Skill\" } ] }";

            // Act
            var character = GenshinJsonParser.ParseCharacter("x", json);

            // Assert
            var talent = Assert.Single(character.SkillTalents);
            Assert.Equal("A", talent.Name);
        }

        [Fact]
        public void ParseCharacter_NoName_Throws()
        {
            // Act & Assert
            Assert.Throws<RemoteApiException>(() => GenshinJsonParser.ParseCharacter("x", "{ \"title\": \"T\" }"));
        }

        [Fact]
        public void ParseCharacter_NotAnObject_Throws()
        {
            // Act & Assert
            Assert.Throws<RemoteApiException>(() => GenshinJsonParser.ParseCharacter("x", "[]"));
        }

        [Fact]
        public void ParseCharacter_InvalidJson_Throws()
        {
            // Act & Assert
            Assert.Throws<RemoteApiException>(() => GenshinJsonParser.ParseCharacter("x", "{ not json"));
        }

        [Fact]
        public void ParseWeapon_ReadsFields()
        {
            // Act
            var weapon = GenshinJsonParser.ParseWeapon("skyward-blade", _samples.WeaponJson);

            // Assert
            Assert.Equal("skyward-blade", weapon.Id);
            Assert.Equal("Skyward Blade", weapon.Name);
            Assert.Equal("Sword", weapon.Type);
            Assert.Equal(5, weapon.Rarity);
            Assert.Equal(46, weapon.BaseAttack);
            Assert.Equal("Energy Recharge", weapon.SubStat);
            Assert.Equal("Sky-Piercing Fang", weapon.PassiveName);
            Assert.Equal("Raises CRIT Rate.", weapon.PassiveDescription);
            Assert.Equal("Gacha", weapon.Location);
        }

        [Fact]
        public void ParseWeapon_MissingNumbers_AreZero()
        {
            // Act
            var weapon = GenshinJsonParser.ParseWeapon("w", "{ \"name\": \"W\" }");

            // Assert
            Assert.Equal(0, weapon.BaseAttack);
            Assert.Equal(0, weapon.Rarity);
        }

        [Fact]
        public void ParseWeapon_NoName_Throws()
        {
            // Act & Assert
            Assert.Throws<RemoteApiException>(() => GenshinJsonParser.ParseWeapon("w", "{ }"));
        }

        [Fact]
        public void ParseIdList_ReadsStrings()
        {
            // Act
            var ids = GenshinJsonParser.ParseIdList(_samples.IdsJson);

            // Assert
            Assert.Equal(new[] { "albedo", "amber", "bennett" }, ids);
        }

        [Fact]
        public void ParseIdList_SkipsNonStringsAndEmptyValues()
        {
            // Act
            var ids = GenshinJsonParser.ParseIdList("[\"a\", 5, \"\", \"b\"]");

            // Assert
            Assert.Equal(new[] { "a", "b" }, ids);
        }

        [Fact]
        public void ParseIdList_NotAnArray_Throws()
        {
            // Act & Assert
            Assert.Throws<RemoteApiException>(() => GenshinJsonParser.ParseIdList("{ }"));
        }
    }

    public class GenshinApiClientTests : IClassFixture<RemoteSamplesFixture>
    {
        private readonly RemoteSamplesFixture _samples;

        public GenshinApiClientTests(RemoteSamplesFixture samples)
        {
            _samples = samples;
        }

        private static GenshinApiClient CreateClient(StubHttpMessageHandler handler) =>
            new GenshinApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://test.local/") });

        [Fact]
        public void Constructor_NullClient_Throws()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new GenshinApiClient(null!));
        }

        [Theory]
        [InlineData("Skyward Blade", "skyward-blade")]
        [InlineData(" Amos' Bow ", "amos-bow")]
        [InlineData("ALBEDO", "albedo")]
        [InlineData("a   b", "a-b")]
        public void NormalizeId_MakesApiFriendlyId(string input, string expected)
        {
            // Act & Assert
            Assert.Equal(expected, GenshinApiClient.NormalizeId(input));
        }

        [Fact]
        public void NormalizeId_Empty_Throws()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => GenshinApiClient.NormalizeId("  "));
        }

        [Fact]
        public async Task GetCharacterIdsAsync_RequestsListAndParsesIt()
        {
            // Arrange
            var handler = StubHttpMessageHandler.Json(_samples.IdsJson);
            var client = CreateClient(handler);

            // Act
            var ids = await client.GetCharacterIdsAsync();

            // Assert
            Assert.Equal(new[] { "albedo", "amber", "bennett" }, ids);
            Assert.Equal("https://test.local/characters", Assert.Single(handler.RequestedUrls));
        }

        [Fact]
        public async Task GetCharacterAsync_NormalizesIdAndParsesResponse()
        {
            // Arrange
            var handler = StubHttpMessageHandler.Json(_samples.AlbedoJson);
            var client = CreateClient(handler);

            // Act
            var character = await client.GetCharacterAsync("Albedo");

            // Assert
            Assert.Equal("Albedo", character.Name);
            Assert.Equal("albedo", character.Id);
            Assert.Equal("https://test.local/characters/albedo", Assert.Single(handler.RequestedUrls));
        }

        [Fact]
        public async Task GetWeaponIdsAsync_RequestsWeaponList()
        {
            // Arrange
            var handler = StubHttpMessageHandler.Json("[\"skyward-blade\"]");
            var client = CreateClient(handler);

            // Act
            var ids = await client.GetWeaponIdsAsync();

            // Assert
            Assert.Equal(new[] { "skyward-blade" }, ids);
            Assert.Equal("https://test.local/weapons", Assert.Single(handler.RequestedUrls));
        }

        [Fact]
        public async Task GetWeaponAsync_NormalizesIdAndParsesResponse()
        {
            // Arrange
            var handler = StubHttpMessageHandler.Json(_samples.WeaponJson);
            var client = CreateClient(handler);

            // Act
            var weapon = await client.GetWeaponAsync("Skyward Blade");

            // Assert
            Assert.Equal(46, weapon.BaseAttack);
            Assert.Equal("https://test.local/weapons/skyward-blade", Assert.Single(handler.RequestedUrls));
        }

        [Fact]
        public async Task NotFound_IsReportedAsRemoteApiException()
        {
            // Arrange
            var client = CreateClient(StubHttpMessageHandler.Status(HttpStatusCode.NotFound));

            // Act
            var exception = await Assert.ThrowsAsync<RemoteApiException>(() => client.GetCharacterAsync("Missing"));

            // Assert
            Assert.Contains("not found", exception.Message);
            Assert.Contains("Missing", exception.Message);
        }

        [Fact]
        public async Task ServerError_IsReportedWithStatusCode()
        {
            // Arrange
            var client = CreateClient(StubHttpMessageHandler.Status(HttpStatusCode.InternalServerError));

            // Act
            var exception = await Assert.ThrowsAsync<RemoteApiException>(() => client.GetWeaponIdsAsync());

            // Assert
            Assert.Contains("500", exception.Message);
        }

        [Fact]
        public async Task NetworkFailure_IsReportedAsRemoteApiException()
        {
            // Arrange
            var client = CreateClient(StubHttpMessageHandler.Throwing(new HttpRequestException("no route to host")));

            // Act
            var exception = await Assert.ThrowsAsync<RemoteApiException>(() => client.GetCharacterIdsAsync());

            // Assert
            Assert.Contains("Cannot reach", exception.Message);
            Assert.Contains("no route to host", exception.Message);
        }

        [Fact]
        public async Task Timeout_IsReportedAsRemoteApiException()
        {
            // Arrange
            var client = CreateClient(StubHttpMessageHandler.Throwing(new TaskCanceledException()));

            // Act
            var exception = await Assert.ThrowsAsync<RemoteApiException>(() => client.GetCharacterIdsAsync());

            // Assert
            Assert.Contains("timed out", exception.Message);
        }

        [Fact]
        public async Task InvalidJsonFromServer_IsReportedAsRemoteApiException()
        {
            // Arrange
            var client = CreateClient(StubHttpMessageHandler.Json("<html>oops</html>"));

            // Act & Assert
            await Assert.ThrowsAsync<RemoteApiException>(() => client.GetCharacterAsync("albedo"));
        }
    }

    public class OfflineGameDataApiTests
    {
        private readonly OfflineGameDataApi _api = new OfflineGameDataApi();

        [Fact]
        public async Task CharacterIds_ContainSamples()
        {
            // Act
            var ids = await _api.GetCharacterIdsAsync();

            // Assert
            Assert.Contains("albedo", ids);
            Assert.Contains("amber", ids);
        }

        [Fact]
        public async Task GetCharacter_ParsesEmbeddedSample()
        {
            // Act
            var albedo = await _api.GetCharacterAsync("Albedo");

            // Assert
            Assert.Equal("Albedo", albedo.Name);
            Assert.Equal(5, albedo.Rarity);
            Assert.Equal(3, albedo.SkillTalents.Count);
        }

        [Fact]
        public async Task GetWeapon_ParsesEmbeddedSample()
        {
            // Act
            var weapon = await _api.GetWeaponAsync("Skyward Blade");

            // Assert
            Assert.Equal("Skyward Blade", weapon.Name);
            Assert.Equal(46, weapon.BaseAttack);
            Assert.Contains("skyward-blade", await _api.GetWeaponIdsAsync());
        }

        [Fact]
        public async Task UnknownCharacter_Throws()
        {
            // Act & Assert
            await Assert.ThrowsAsync<RemoteApiException>(() => _api.GetCharacterAsync("nobody"));
        }

        [Fact]
        public async Task UnknownWeapon_Throws()
        {
            // Act & Assert
            await Assert.ThrowsAsync<RemoteApiException>(() => _api.GetWeaponAsync("nothing"));
        }
    }
}