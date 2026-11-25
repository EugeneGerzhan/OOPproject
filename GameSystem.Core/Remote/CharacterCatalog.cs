using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Infra.WebApi;

namespace GameSystem.Core.Remote
{
    /// <summary>A talent as the GUI shows it.</summary>
    public record TalentCard(string Name, string Kind, string Description);

    /// <summary>
    /// A character as the GUI shows it: everything is ready to bind (no JSON, no nulls).
    /// The graphical interface only has to make a view model out of it.
    /// </summary>
    public record CharacterCard(
        string Id, string Name, string Title, string Vision, string WeaponType, int Rarity,
        string Nation, string Affiliation, string Description, string ImageUrl,
        IReadOnlyList<TalentCard> Talents, IReadOnlyList<TalentCard> Passives)
    {
        public static CharacterCard From(RemoteCharacter remote)
        {
            ArgumentNullException.ThrowIfNull(remote);

            return new CharacterCard(
                remote.Id, remote.Name, remote.Title, remote.Vision, remote.WeaponType, remote.Rarity,
                remote.Nation, remote.Affiliation, remote.Description, remote.ImageUrl,
                remote.SkillTalents.Select(t => new TalentCard(t.Name, t.Unlock, t.Description)).ToList(),
                remote.PassiveTalents.Select(t => new TalentCard(t.Name, t.Unlock, t.Description)).ToList());
        }
    }

    public enum CatalogSort
    {
        Name,
        Rarity,
        Vision
    }

    /// <summary>What the user asked for in the catalog; every part is optional.</summary>
    public record CatalogFilter(
        string? Text = null, string? Vision = null, string? WeaponType = null,
        int? MinRarity = null, CatalogSort Sort = CatalogSort.Name);

    /// <summary>The result of an update of the catalog.</summary>
    public record CatalogRefreshResult(int Loaded, IReadOnlyList<string> Failed);

    /// <summary>
    /// The catalog of characters of the remote database: it loads the cards, keeps them in memory
    /// and answers searches. It knows only <see cref="IGameDataApi"/>, so a GUI and a CLI can use it alike.
    /// </summary>
    public sealed class CharacterCatalog
    {
        private readonly IGameDataApi _api;
        private readonly Dictionary<string, CharacterCard> _cards = new Dictionary<string, CharacterCard>(StringComparer.OrdinalIgnoreCase);

        public CharacterCatalog(IGameDataApi api)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
        }

        /// <summary>All loaded cards ordered by name.</summary>
        public IReadOnlyList<CharacterCard> Cards =>
            _cards.Values.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>
        /// Updates the catalog: asks the service for the list and loads the card of every character
        /// (several at a time). A character that cannot be loaded is reported, it does not stop the update.
        /// If the list itself cannot be loaded, the old cards stay as they were.
        /// </summary>
        public async Task<CatalogRefreshResult> RefreshAsync(int maxParallel = 6, CancellationToken cancellationToken = default)
        {
            if (maxParallel < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxParallel), "At least one request at a time is needed.");
            }

            var ids = await _api.GetCharacterIdsAsync(cancellationToken);

            var loaded = new List<CharacterCard>();
            var failed = new List<string>();
            using var gate = new SemaphoreSlim(maxParallel);

            var tasks = ids.Select(async id =>
            {
                await gate.WaitAsync(cancellationToken);
                try
                {
                    var remote = await _api.GetCharacterAsync(id, cancellationToken);
                    lock (loaded)
                    {
                        loaded.Add(CharacterCard.From(remote));
                    }
                }
                catch (RemoteApiException)
                {
                    lock (failed)
                    {
                        failed.Add(id);
                    }
                }
                finally
                {
                    gate.Release();
                }
            }).ToList();

            await Task.WhenAll(tasks);

            _cards.Clear();
            foreach (var card in loaded)
            {
                _cards[card.Id] = card;
            }

            return new CatalogRefreshResult(loaded.Count, failed.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList());
        }

        /// <summary>The loaded card or null.</summary>
        public CharacterCard? Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }
            return _cards.TryGetValue(GenshinApiClient.NormalizeId(id), out var card) ? card : null;
        }

        /// <summary>The card from the memory, or from the service (and then kept in the memory).</summary>
        public async Task<CharacterCard> GetAsync(string id, CancellationToken cancellationToken = default)
        {
            string key = GenshinApiClient.NormalizeId(id);
            if (_cards.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var card = CharacterCard.From(await _api.GetCharacterAsync(key, cancellationToken));
            _cards[card.Id] = card;
            return card;
        }

        /// <summary>Searches the loaded cards; the filter parts are combined with "and".</summary>
        public IReadOnlyList<CharacterCard> Search(CatalogFilter? filter = null)
        {
            filter ??= new CatalogFilter();
            IEnumerable<CharacterCard> query = _cards.Values;

            if (!string.IsNullOrWhiteSpace(filter.Text))
            {
                string text = filter.Text.Trim();
                query = query.Where(c =>
                    c.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || c.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || c.Id.Contains(text, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrWhiteSpace(filter.Vision))
            {
                query = query.Where(c => string.Equals(c.Vision, filter.Vision.Trim(), StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrWhiteSpace(filter.WeaponType))
            {
                query = query.Where(c => string.Equals(c.WeaponType, filter.WeaponType.Trim(), StringComparison.OrdinalIgnoreCase));
            }
            if (filter.MinRarity.HasValue)
            {
                query = query.Where(c => c.Rarity >= filter.MinRarity.Value);
            }

            query = filter.Sort switch
            {
                CatalogSort.Rarity => query.OrderByDescending(c => c.Rarity).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase),
                CatalogSort.Vision => query.OrderBy(c => c.Vision, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase),
                _ => query.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            };

            return query.ToList();
        }
    }
}