using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Infra.Data;

namespace ProjectTests.Mocks
{
    /// <summary>Bytes of pictures of every supported format (and one that is not a picture).</summary>
    public static class PngBytes
    {
        public static readonly byte[] Pixel = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

        public static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };

        public static readonly byte[] Gif = Encoding.ASCII.GetBytes("GIF89a\u0001\u0000\u0001\u0000");

        public static readonly byte[] Webp = Encoding.ASCII.GetBytes("RIFF\0\0\0\0WEBPVP8 ");

        public static readonly byte[] Html = Encoding.UTF8.GetBytes("<html>Error</html>");
    }

    /// <summary>A picture source that returns prepared bytes, counts the requests and can fail.</summary>
    public class FakeImageSource : IImageSource
    {
        public int Calls { get; private set; }

        public List<string> Urls { get; } = new List<string>();

        public byte[] Data { get; set; } = PngBytes.Pixel;

        public Exception? Failure { get; set; }

        public Task<DownloadedImage> DownloadAsync(string url, CancellationToken cancellationToken = default)
        {
            Calls++;
            Urls.Add(url);

            if (Failure != null)
            {
                throw Failure;
            }
            return Task.FromResult(new DownloadedImage(Data, "image/png"));
        }
    }
}