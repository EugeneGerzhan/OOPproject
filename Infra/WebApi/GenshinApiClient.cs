using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Infra.WebApi
{
    /// <summary>HTTP client for https://genshin.jmp.blue (BaseAddress is set by the creator of the HttpClient).</summary>
    public class GenshinApiClient : IGameDataApi
    {
        private readonly HttpClient _http;

        public GenshinApiClient(HttpClient http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        /// <summary>"Skyward Blade" -> "skyward-blade", "Amos' Bow" -> "amos-bow".</summary>
        public static string NormalizeId(string id)
        {
            string normalized = (id ?? string.Empty).Trim().ToLowerInvariant()
                .Replace("'", string.Empty)
                .Replace("\u2019", string.Empty);
            normalized = Regex.Replace(normalized, @"\s+", "-");

            if (normalized.Length == 0)
            {
                throw new ArgumentException("Id cannot be empty.");
            }
            return normalized;
        }

        public async Task<IReadOnlyList<string>> GetCharacterIdsAsync(CancellationToken cancellationToken = default)
        {
            string json = await FetchAsync("characters", "The character list", cancellationToken);
            return GenshinJsonParser.ParseIdList(json);
        }

        public async Task<RemoteCharacter> GetCharacterAsync(string id, CancellationToken cancellationToken = default)
        {
            string key = NormalizeId(id);
            string json = await FetchAsync($"characters/{Uri.EscapeDataString(key)}", $"Character '{id.Trim()}'", cancellationToken);
            var character = GenshinJsonParser.ParseCharacter(key, json);

            // The service describes images with endpoints like /characters/{id}/card: use it when the answer has no link.
            if (character.ImageUrl.Length == 0 && _http.BaseAddress != null)
            {
                string link = new Uri(_http.BaseAddress, $"characters/{Uri.EscapeDataString(key)}/card").ToString();
                character = character with { ImageUrl = link };
            }
            return character;
        }

        public async Task<IReadOnlyList<string>> GetWeaponIdsAsync(CancellationToken cancellationToken = default)
        {
            string json = await FetchAsync("weapons", "The weapon list", cancellationToken);
            return GenshinJsonParser.ParseIdList(json);
        }

        public async Task<RemoteWeapon> GetWeaponAsync(string id, CancellationToken cancellationToken = default)
        {
            string key = NormalizeId(id);
            string json = await FetchAsync($"weapons/{Uri.EscapeDataString(key)}", $"Weapon '{id.Trim()}'", cancellationToken);
            return GenshinJsonParser.ParseWeapon(key, json);
        }

        private async Task<string> FetchAsync(string path, string what, CancellationToken cancellationToken)
        {
            try
            {
                using var response = await _http.GetAsync(path, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new RemoteApiException($"{what} was not found in the remote database.");
                }
                if (!response.IsSuccessStatusCode)
                {
                    throw new RemoteApiException($"The remote service returned {(int)response.StatusCode} {response.ReasonPhrase}.");
                }

                return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new RemoteApiException("Cannot reach the remote service: " + ex.Message, ex);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new RemoteApiException("The request to the remote service timed out.", ex);
            }
        }
    }
}