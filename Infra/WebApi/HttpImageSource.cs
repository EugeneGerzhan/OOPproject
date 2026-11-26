using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Infra.Data;

namespace Infra.WebApi
{
    /// <summary>Downloads pictures over HTTP (the address of the picture is absolute, so any server works).</summary>
    public sealed class HttpImageSource : IImageSource
    {
        private const long MaxBytes = 10 * 1024 * 1024;

        private readonly HttpClient _http;

        public HttpImageSource(HttpClient http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        public async Task<DownloadedImage> DownloadAsync(string url, CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new RemoteApiException("The picture was not found on the remote service.");
                }
                if (!response.IsSuccessStatusCode)
                {
                    throw new RemoteApiException($"The remote service returned {(int)response.StatusCode} {response.ReasonPhrase}.");
                }
                if (response.Content.Headers.ContentLength > MaxBytes)
                {
                    throw new RemoteApiException("The picture is too large.");
                }

                byte[] data = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                if (data.Length > MaxBytes)
                {
                    throw new RemoteApiException("The picture is too large.");
                }

                return new DownloadedImage(data, response.Content.Headers.ContentType?.MediaType);
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

    /// <summary>For the offline mode: every picture is a tiny built-in PNG.</summary>
    public sealed class OfflineImageSource : IImageSource
    {
        private static readonly byte[] Pixel = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

        public Task<DownloadedImage> DownloadAsync(string url, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DownloadedImage((byte[])Pixel.Clone(), "image/png"));
    }
}