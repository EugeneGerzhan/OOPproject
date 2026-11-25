using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameSystem.Core.Remote;
using Infra.WebApi;
using ProjectTests.Mocks;
using Xunit;

namespace ProjectTests.CatalogTests
{
    public class CharacterCatalogTests
    {
        private readonly FakeGameDataApi _api = new FakeGameDataApi();

        public CharacterCatalogTests()
        {
            _api.Characters["albedo"] = RemoteSamples.Albedo();                       // Geo, Sword, 5 stars
            _api.Characters["amber"] = RemoteSamples.Simple("amber", "Amber");       // Pyro, Bow, 4 stars
            _api.Characters["bennett"] = RemoteSamples.Simple("bennett", "Bennett"); // Pyro, Bow, 4 stars
        }

        // ---------- the card ----------

        [Fact]
        public void Card_CopiesTheProfileAndTheTalents()
        {
            // Act
            var card = CharacterCard.From(RemoteSamples.Albedo() with { ImageUrl = "https://example.com/a.png" });

            // Assert
            Assert.Equal("albedo", card.Id);
            Assert.Equal("Albedo", card.Name);
            Assert.Equal("Kreideprinz", card.Title);
            Assert.Equal("Geo", card.Vision);
            Assert.Equal("Sword", card.WeaponType);
            Assert.Equal(5, card.Rarity);
            Assert.Equal("https://example.com/a.png", card.ImageUrl);
            Assert.Equal(3, card.Talents.Count);
            Assert.Equal("Normal Attack", card.Talents[0].Kind);
            Assert.Single(card.Passives);
        }

        [Fact]
        public void Card_Null_Throws()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => CharacterCard.From(null!));
        }

        // ---------- refresh ----------

        [Fact]
        public async Task Refresh_LoadsEveryCharacter_OrderedByName()
        {
            // Arrange
            var catalog = new CharacterCatalog(_api);

            // Act
            var result = await catalog.RefreshAsync();

            // Assert
            Assert.Equal(3, result.Loaded);
            Assert.Empty(result.Failed);
            Assert.Equal(new[] { "Albedo", "Amber", "Bennett" }, catalog.Cards.Select(c => c.Name));
        }

        [Fact]
        public async Task Refresh_ACharacterThatCannotBeLoaded_IsReportedAndTheRestIsKept()
        {
            // Arrange
            var catalog = new CharacterCatalog(new ApiWithABrokenCharacter());

            // Act
            var result = await catalog.RefreshAsync();

            // Assert
            Assert.Equal(1, result.Loaded);
            Assert.Equal(new[] { "broken" }, result.Failed);
            Assert.Single(catalog.Cards);
        }

        [Fact]
        public async Task Refresh_WhenTheListCannotBeLoaded_KeepsTheOldCards()
        {
            // Arrange
            var api = new SwitchableApi(_api);
            var catalog = new CharacterCatalog(api);
            await catalog.RefreshAsync();
            api.Fail = true;

            // Act & Assert
            await Assert.ThrowsAsync<RemoteApiException>(() => catalog.RefreshAsync());
            Assert.Equal(3, catalog.Cards.Count);
        }

        [Fact]
        public async Task Refresh_ReplacesTheOldCards()
        {
            // Arrange
            var catalog = new CharacterCatalog(_api);
            await catalog.RefreshAsync();
            _api.Characters.Remove("bennett");

            // Act
            await catalog.RefreshAsync();

            // Assert
            Assert.Equal(2, catalog.Cards.Count);
        }

        [Fact]
        public async Task Refresh_InvalidParallelism_Throws()
        {
            // Arrange
            var catalog = new CharacterCatalog(_api);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => catalog.RefreshAsync(0));
        }

        // ---------- get and find ----------

        [Fact]
        public async Task GetAsync_AsksTheServiceOnce_AndThenUsesTheMemory()
        {
            // Arrange
            var catalog = new CharacterCatalog(_api);

            // Act
            var first = await catalog.GetAsync("Albedo");
            var second = await catalog.GetAsync("albedo");

            // Assert
            Assert.Same(first, second);
            Assert.Single(_api.Calls.Where(c => c == "characters/albedo"));
        }

        [Fact]
        public async Task GetAsync_UnknownCharacter_Throws()
        {
            // Arrange
            var catalog = new CharacterCatalog(_api);

            // Act & Assert
            await Assert.ThrowsAsync<RemoteApiException>(() => catalog.GetAsync("nobody"));
        }

        [Fact]
        public async Task Find_ReturnsALoadedCardOrNull()
        {
            // Arrange
            var catalog = new CharacterCatalog(_api);
            await catalog.RefreshAsync();

            // Act & Assert
            Assert.Equal("Albedo", catalog.Find("ALBEDO")!.Name);
            Assert.Null(catalog.Find("nobody"));
            Assert.Null(catalog.Find("  "));
        }

        // ---------- search ----------

        private async Task<CharacterCatalog> LoadedCatalog()
        {
            var catalog = new CharacterCatalog(_api);
            await catalog.RefreshAsync();
            return catalog;
        }

        [Fact]
        public async Task Search_WithoutAFilter_ReturnsEverythingByName()
        {
            // Arrange
            var catalog = await LoadedCatalog();

            // Act & Assert
            Assert.Equal(new[] { "Albedo", "Amber", "Bennett" }, catalog.Search().Select(c => c.Name));
        }

        [Fact]
        public async Task Search_ByText_MatchesTheNameTheTitleAndTheId()
        {
            // Arrange
            var catalog = await LoadedCatalog();

            // Act & Assert
            Assert.Equal(new[] { "Albedo" }, catalog.Search(new CatalogFilter(Text: "ALB")).Select(c => c.Name));
            Assert.Equal(new[] { "Albedo" }, catalog.Search(new CatalogFilter(Text: "kreide")).Select(c => c.Name));
            Assert.Equal(new[] { "Bennett" }, catalog.Search(new CatalogFilter(Text: "bennett")).Select(c => c.Name));
        }

        [Fact]
        public async Task Search_ByVisionAndWeapon()
        {
            // Arrange
            var catalog = await LoadedCatalog();

            // Act & Assert
            Assert.Equal(new[] { "Amber", "Bennett" }, catalog.Search(new CatalogFilter(Vision: "pyro")).Select(c => c.Name));
            Assert.Equal(new[] { "Albedo" }, catalog.Search(new CatalogFilter(WeaponType: "sword")).Select(c => c.Name));
            Assert.Empty(catalog.Search(new CatalogFilter(Vision: "geo", WeaponType: "bow")));
        }

        [Fact]
        public async Task Search_ByMinimumRarity()
        {
            // Arrange
            var catalog = await LoadedCatalog();

            // Act & Assert
            Assert.Equal(new[] { "Albedo" }, catalog.Search(new CatalogFilter(MinRarity: 5)).Select(c => c.Name));
            Assert.Equal(3, catalog.Search(new CatalogFilter(MinRarity: 4)).Count);
        }

        [Fact]
        public async Task Search_SortedByRarity_PutsTheBestFirst()
        {
            // Arrange
            var catalog = await LoadedCatalog();

            // Act
            var names = catalog.Search(new CatalogFilter(Sort: CatalogSort.Rarity)).Select(c => c.Name);

            // Assert
            Assert.Equal(new[] { "Albedo", "Amber", "Bennett" }, names);
        }

        [Fact]
        public async Task Search_SortedByVision()
        {
            // Arrange
            _api.Characters["xiao"] = RemoteSamples.Simple("xiao", "Xiao") with { Vision = "Anemo" };
            var catalog = await LoadedCatalog();

            // Act
            var names = catalog.Search(new CatalogFilter(Sort: CatalogSort.Vision)).Select(c => c.Name);

            // Assert
            Assert.Equal(new[] { "Xiao", "Albedo", "Amber", "Bennett" }, names);
        }

        // ---------- test doubles ----------

        private sealed class ApiWithABrokenCharacter : IGameDataApi
        {
            public Task<IReadOnlyList<string>> GetCharacterIdsAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<string>>(new[] { "albedo", "broken" });

            public Task<RemoteCharacter> GetCharacterAsync(string id, CancellationToken cancellationToken = default)
            {
                if (id == "broken")
                {
                    throw new RemoteApiException("Cannot reach the remote service.");
                }
                return Task.FromResult(RemoteSamples.Albedo());
            }

            public Task<IReadOnlyList<string>> GetWeaponIdsAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

            public Task<RemoteWeapon> GetWeaponAsync(string id, CancellationToken cancellationToken = default) =>
                throw new RemoteApiException("No weapons.");
        }

        private sealed class SwitchableApi : IGameDataApi
        {
            private readonly IGameDataApi _inner;

            public SwitchableApi(IGameDataApi inner)
            {
                _inner = inner;
            }

            public bool Fail { get; set; }

            public Task<IReadOnlyList<string>> GetCharacterIdsAsync(CancellationToken cancellationToken = default)
            {
                if (Fail)
                {
                    throw new RemoteApiException("The service is down.");
                }
                return _inner.GetCharacterIdsAsync(cancellationToken);
            }

            public Task<RemoteCharacter> GetCharacterAsync(string id, CancellationToken cancellationToken = default) =>
                _inner.GetCharacterAsync(id, cancellationToken);

            public Task<IReadOnlyList<string>> GetWeaponIdsAsync(CancellationToken cancellationToken = default) =>
                _inner.GetWeaponIdsAsync(cancellationToken);

            public Task<RemoteWeapon> GetWeaponAsync(string id, CancellationToken cancellationToken = default) =>
                _inner.GetWeaponAsync(id, cancellationToken);
        }
    }
}