using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Infra.Data
{
    /// <summary>The bytes of a downloaded picture and the media type the server announced (if any).</summary>
    public sealed record DownloadedImage(byte[] Data, string? ContentType);

    /// <summary>Where pictures come from (HTTP, test data...). The cache does not care.</summary>
    public interface IImageSource
    {
        Task<DownloadedImage> DownloadAsync(string url, CancellationToken cancellationToken = default);
    }

    /// <summary>A picture in a local file. Stale means: it is expired, but the service did not answer, so the old file is used.</summary>
    public sealed record CachedImage(string FilePath, long Size, bool FromCache, bool Stale = false);

    /// <summary>A file of the cache.</summary>
    public sealed record CachedImageInfo(string Key, string FilePath, long Size, DateTime SavedAtUtc);

    /// <summary>Recognizes the supported picture formats by their first bytes (the name or the media type can lie).</summary>
    public static class ImageFormats
    {
        /// <returns>The extension with a dot (".png", ".jpg", ".gif", ".webp"), or null when the bytes are not a supported picture.</returns>
        public static string? Detect(byte[] data)
        {
            if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            {
                return ".png";
            }
            if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            {
                return ".jpg";
            }
            if (data.Length >= 6 && data[0] == 'G' && data[1] == 'I' && data[2] == 'F' && data[3] == '8')
            {
                return ".gif";
            }
            if (data.Length >= 12 && data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F'
                && data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P')
            {
                return ".webp";
            }
            return null;
        }
    }

    /// <summary>
    /// The buffer of pictures: a picture is downloaded once and kept in a local file, the next requests read the file,
    /// so the number of requests to the server shrinks.
    /// </summary>
    /// <remarks>
    /// Files are named after a safe form of the key and the real format of the picture. A file is written through a
    /// temporary file, so a crash never leaves a half-written picture. Only real pictures are stored: an error page
    /// that a server returns instead of a picture is rejected. With <c>maxAge</c> a file expires; if the server cannot
    /// be reached then, the old file is still used.
    /// </remarks>
    public sealed class ImageCache
    {
        private static readonly string[] Extensions = { ".png", ".jpg", ".gif", ".webp" };

        private readonly IImageSource _source;
        private readonly TimeSpan? _maxAge;
        private readonly Func<DateTime> _utcNow;

        /// <param name="folder">The folder of the cache (created when the first picture is stored).</param>
        /// <param name="source">Where pictures are downloaded from.</param>
        /// <param name="maxAge">How long a file is fresh; null means for ever.</param>
        /// <param name="utcNow">The clock (replaceable in tests).</param>
        public ImageCache(string folder, IImageSource source, TimeSpan? maxAge = null, Func<DateTime>? utcNow = null)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new ArgumentException("The folder cannot be empty.", nameof(folder));
            }
            if (maxAge.HasValue && maxAge.Value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAge), "The maximum age must be positive.");
            }

            Folder = Path.GetFullPath(folder);
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _maxAge = maxAge;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public string Folder { get; }

        /// <summary>The size of all pictures of the cache.</summary>
        public long TotalBytes => ManagedFiles().Sum(file => new FileInfo(file).Length);

        /// <summary>The path of a fresh file of the cache, when there is one.</summary>
        public bool TryGetCached(string key, out string filePath)
        {
            string? existing = FindFile(SafeKey(key));
            if (existing != null && !IsExpired(existing))
            {
                filePath = existing;
                return true;
            }

            filePath = string.Empty;
            return false;
        }

        /// <summary>The picture in a local file: the fresh file of the cache, or a newly downloaded one.</summary>
        /// <param name="key">The name of the picture (for example "albedo-portrait").</param>
        /// <param name="url">Where to download it from when the cache has no fresh file.</param>
        /// <exception cref="InvalidDataException">The server returned something that is not a supported picture.</exception>
        public async Task<CachedImage> GetAsync(string key, string url, CancellationToken cancellationToken = default)
        {
            string safeKey = SafeKey(key);
            string? existing = FindFile(safeKey);

            if (existing != null && !IsExpired(existing))
            {
                return new CachedImage(existing, new FileInfo(existing).Length, FromCache: true);
            }

            try
            {
                DownloadedImage downloaded = await _source.DownloadAsync(url, cancellationToken).ConfigureAwait(false);

                string? extension = ImageFormats.Detect(downloaded.Data);
                if (extension == null)
                {
                    throw new InvalidDataException("The service did not return a supported picture (PNG, JPEG, GIF or WebP).");
                }

                string path = Write(safeKey, extension, downloaded.Data);
                return new CachedImage(path, downloaded.Data.Length, FromCache: false);
            }
            catch (Exception ex) when (existing != null && ex is not OperationCanceledException)
            {
                // An old picture is better than none.
                return new CachedImage(existing, new FileInfo(existing).Length, FromCache: true, Stale: true);
            }
        }

        /// <summary>The files of the cache.</summary>
        public IReadOnlyList<CachedImageInfo> List() =>
            ManagedFiles()
                .Select(file => new FileInfo(file))
                .OrderBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
                .Select(info => new CachedImageInfo(
                    Path.GetFileNameWithoutExtension(info.Name), info.FullName, info.Length, info.LastWriteTimeUtc))
                .ToList();

        /// <summary>Deletes all pictures of the cache.</summary>
        /// <returns>How many files were deleted.</returns>
        public int Clear()
        {
            int removed = 0;
            foreach (string file in ManagedFiles().ToList())
            {
                File.Delete(file);
                removed++;
            }
            return removed;
        }

        // ---------- helpers ----------

        private string Write(string key, string extension, byte[] data)
        {
            Directory.CreateDirectory(Folder);

            string target = Path.Combine(Folder, key + extension);
            string temp = target + ".tmp";
            File.WriteAllBytes(temp, data);

            // A picture of the same key in another format (png before, webp now) must not stay behind.
            foreach (string old in Extensions.Select(e => Path.Combine(Folder, key + e)).Where(p => p != target && File.Exists(p)))
            {
                File.Delete(old);
            }

            File.Move(temp, target, overwrite: true);
            return target;
        }

        private string? FindFile(string key)
        {
            if (!Directory.Exists(Folder))
            {
                return null;
            }

            foreach (string extension in Extensions)
            {
                string candidate = Path.Combine(Folder, key + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return null;
        }

        private bool IsExpired(string path) =>
            _maxAge.HasValue && _utcNow() - File.GetLastWriteTimeUtc(path) > _maxAge.Value;

        private IEnumerable<string> ManagedFiles() =>
            Directory.Exists(Folder)
                ? Directory.EnumerateFiles(Folder).Where(IsManaged)
                : Enumerable.Empty<string>();

        private static bool IsManaged(string path) =>
            Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

        private static string SafeKey(string key)
        {
            string safe = Regex.Replace((key ?? string.Empty).Trim().ToLowerInvariant(), @"[^\p{L}\p{N}_-]+", "-").Trim('-');
            if (safe.Length == 0)
            {
                throw new ArgumentException("The key of a picture cannot be empty.", nameof(key));
            }
            return safe.Length > 120 ? safe.Substring(0, 120) : safe;
        }
    }
}