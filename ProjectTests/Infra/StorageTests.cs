using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Infra.Data;
using Infra.WebApi;
using ProjectTests.Mocks;
using Xunit;

namespace ProjectTests.PersistenceTests
{
    public class AppPathsTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"oop-paths-{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        [Fact]
        public void Constructor_BuildsTheFilePaths()
        {
            // Act
            var paths = new AppPaths(_root);

            // Assert
            Assert.Equal(Path.GetFullPath(_root), paths.DataDirectory);
            Assert.Equal(Path.Combine(Path.GetFullPath(_root), "saves.json"), paths.SavesFile);
            Assert.Equal(Path.Combine(Path.GetFullPath(_root), "images"), paths.ImagesDirectory);
        }

        [Fact]
        public void Constructor_EmptyDirectory_Throws()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new AppPaths("  "));
        }

        [Fact]
        public void ForCurrentUser_UsesTheEnvironmentVariableWhenItIsSet()
        {
            // Arrange
            string? old = Environment.GetEnvironmentVariable(AppPaths.DataDirectoryVariable);
            try
            {
                Environment.SetEnvironmentVariable(AppPaths.DataDirectoryVariable, _root);

                // Act
                var paths = AppPaths.ForCurrentUser();

                // Assert
                Assert.Equal(Path.GetFullPath(_root), paths.DataDirectory);
            }
            finally
            {
                Environment.SetEnvironmentVariable(AppPaths.DataDirectoryVariable, old);
            }
        }

        [Fact]
        public void ForCurrentUser_WithoutTheVariable_UsesAFolderNamedAfterTheApplication()
        {
            // Arrange
            string? old = Environment.GetEnvironmentVariable(AppPaths.DataDirectoryVariable);
            try
            {
                Environment.SetEnvironmentVariable(AppPaths.DataDirectoryVariable, null);

                // Act
                var paths = AppPaths.ForCurrentUser();

                // Assert
                Assert.EndsWith(AppPaths.AppFolderName, paths.DataDirectory);
            }
            finally
            {
                Environment.SetEnvironmentVariable(AppPaths.DataDirectoryVariable, old);
            }
        }

        [Fact]
        public void MigrateLegacySaves_CopiesTheOldFileOnce()
        {
            // Arrange
            Directory.CreateDirectory(_root);
            string legacy = Path.Combine(_root, "old.json");
            File.WriteAllText(legacy, "{ \"marker\": 1 }");
            var paths = new AppPaths(Path.Combine(_root, "data"));

            // Act
            bool first = paths.MigrateLegacySaves(legacy);
            File.WriteAllText(legacy, "{ \"marker\": 2 }");
            bool second = paths.MigrateLegacySaves(legacy);

            // Assert
            Assert.True(first);
            Assert.False(second);
            Assert.Contains("\"marker\": 1", File.ReadAllText(paths.SavesFile));
        }

        [Fact]
        public void MigrateLegacySaves_WithoutAnOldFile_DoesNothing()
        {
            // Arrange
            var paths = new AppPaths(Path.Combine(_root, "data"));

            // Act & Assert
            Assert.False(paths.MigrateLegacySaves(Path.Combine(_root, "missing.json")));
            Assert.False(File.Exists(paths.SavesFile));
        }
    }

    public class SafeJsonStorageTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"oop-storage-{Guid.NewGuid():N}");
        private readonly string _path;

        public SafeJsonStorageTests()
        {
            _path = Path.Combine(_root, "data", "saves.json");
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        private static Repository WithTextSave(string name)
        {
            var repository = new Repository();
            repository.TextSaves[name] = new TextSave { SaveName = name };
            return repository;
        }

        [Fact]
        public void Save_CreatesMissingDirectories()
        {
            // Arrange
            var storage = new JsonRepositoryStorage(_path);

            // Act
            storage.Save(new Repository());

            // Assert
            Assert.True(File.Exists(_path));
        }

        [Fact]
        public void Save_LeavesNoTemporaryFile()
        {
            // Arrange
            var storage = new JsonRepositoryStorage(_path);

            // Act
            storage.Save(new Repository());
            storage.Save(new Repository());

            // Assert
            Assert.False(File.Exists(_path + ".tmp"));
        }

        [Fact]
        public void Save_KeepsThePreviousVersionAsABackup()
        {
            // Arrange
            var storage = new JsonRepositoryStorage(_path);
            storage.Save(WithTextSave("first"));

            // Act
            storage.Save(WithTextSave("second"));

            // Assert
            string backup = File.ReadAllText(_path + ".bak");
            Assert.Contains("\"first\"", backup);
            Assert.DoesNotContain("\"second\"", backup);
            Assert.Contains("\"second\"", File.ReadAllText(_path));
        }

        [Fact]
        public void Load_ADamagedFile_FallsBackToTheBackup()
        {
            // Arrange
            var storage = new JsonRepositoryStorage(_path);
            storage.Save(WithTextSave("first"));
            storage.Save(WithTextSave("second"));
            File.WriteAllText(_path, "{ broken");

            // Act
            var repository = storage.Load();

            // Assert
            Assert.True(repository.TextSaves.ContainsKey("first"));
        }

        [Fact]
        public void Load_ADamagedFileWithoutABackup_Throws()
        {
            // Arrange
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, "{ broken");
            var storage = new JsonRepositoryStorage(_path);

            // Act & Assert
            Assert.ThrowsAny<JsonException>(() => storage.Load());
        }

        [Fact]
        public void Load_WhenTheFileAndTheBackupAreDamaged_Throws()
        {
            // Arrange
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, "{ broken");
            File.WriteAllText(_path + ".bak", "{ also broken");
            var storage = new JsonRepositoryStorage(_path);

            // Act & Assert
            Assert.ThrowsAny<JsonException>(() => storage.Load());
        }

        [Fact]
        public void Save_WritesTheFormatVersion()
        {
            // Arrange
            var storage = new JsonRepositoryStorage(_path);

            // Act
            storage.Save(new Repository());

            // Assert
            Assert.Contains("\"Version\": 1", File.ReadAllText(_path));
        }

        [Fact]
        public void Load_AFileFromANewerVersion_IsRefused()
        {
            // Arrange
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, "{ \"Version\": 99 }");
            var storage = new JsonRepositoryStorage(_path);

            // Act
            var exception = Assert.Throws<InvalidDataException>(() => storage.Load());

            // Assert
            Assert.Contains("newer version", exception.Message);
        }

        [Fact]
        public void Load_AFileWithoutAVersion_IsReadAsTheCurrentFormat()
        {
            // Arrange
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, "{ \"TextSaves\": {} }");
            var storage = new JsonRepositoryStorage(_path);

            // Act
            var repository = storage.Load();

            // Assert
            Assert.Equal(RepositoryFormat.CurrentVersion, repository.Version);
        }

        [Fact]
        public void ManySaves_KeepTheLatestData()
        {
            // Arrange
            var storage = new JsonRepositoryStorage(_path);

            // Act
            for (int i = 1; i <= 5; i++)
            {
                var repository = storage.Load();
                repository.TextSaves[$"save{i}"] = new TextSave { SaveName = $"save{i}" };
                storage.Save(repository);
            }

            // Assert
            Assert.Equal(5, storage.Load().TextSaves.Count);
        }
    }

    public class LastSaveTests
    {
        private static CharacterSave Save(string name) => new CharacterSave { SaveName = name };

        [Fact]
        public void WhenNothingWasSaved_ThereIsNoLastSave()
        {
            // Arrange
            var manager = new SaveManager(new InMemoryStorage());

            // Act & Assert
            Assert.Null(manager.LastCharacterSaveName);
        }

        [Fact]
        public void SaveCharacters_RemembersTheLastSave()
        {
            // Arrange
            var manager = new SaveManager(new InMemoryStorage());

            // Act
            manager.SaveCharacters(Save("a"));
            manager.SaveCharacters(Save("b"));

            // Assert
            Assert.Equal("b", manager.LastCharacterSaveName);

            manager.SaveCharacters(Save("a"));
            Assert.Equal("a", manager.LastCharacterSaveName);
        }

        [Fact]
        public void TheLastSave_KeepsItsCase()
        {
            // Arrange
            var manager = new SaveManager(new InMemoryStorage());

            // Act
            manager.SaveCharacters(Save("Arena"));

            // Assert
            Assert.Equal("Arena", manager.LastCharacterSaveName);
        }

        [Fact]
        public void SavedDocuments_DoNotChangeTheLastGameSave()
        {
            // Arrange
            var manager = new SaveManager(new InMemoryStorage());
            manager.SaveCharacters(Save("game"));

            // Act
            manager.SaveText(new TextSave { SaveName = "doc" });

            // Assert
            Assert.Equal("game", manager.LastCharacterSaveName);
        }

        [Fact]
        public void APointerToAMissingSave_IsIgnored()
        {
            // Arrange
            var storage = new InMemoryStorage();
            storage.Load().LastCharacterSave = "ghost";
            var manager = new SaveManager(storage);

            // Act & Assert
            Assert.Null(manager.LastCharacterSaveName);
        }

        [Fact]
        public void TheLastSave_SurvivesTheJsonFile()
        {
            // Arrange
            string path = Path.Combine(Path.GetTempPath(), $"oop-last-{Guid.NewGuid():N}.json");
            try
            {
                new SaveManager(new JsonRepositoryStorage(path)).SaveCharacters(Save("game"));

                // Act
                var name = new SaveManager(new JsonRepositoryStorage(path)).LastCharacterSaveName;

                // Assert
                Assert.Equal("game", name);
            }
            finally
            {
                foreach (string file in new[] { path, path + ".bak", path + ".tmp" })
                {
                    if (File.Exists(file)) File.Delete(file);
                }
            }
        }
    }

    public class ImageFormatsTests
    {
        [Fact]
        public void Detect_RecognizesEverySupportedFormat()
        {
            // Act & Assert
            Assert.Equal(".png", ImageFormats.Detect(PngBytes.Pixel));
            Assert.Equal(".jpg", ImageFormats.Detect(PngBytes.Jpeg));
            Assert.Equal(".gif", ImageFormats.Detect(PngBytes.Gif));
            Assert.Equal(".webp", ImageFormats.Detect(PngBytes.Webp));
        }

        [Fact]
        public void Detect_RejectsEverythingElse()
        {
            // Act & Assert
            Assert.Null(ImageFormats.Detect(PngBytes.Html));
            Assert.Null(ImageFormats.Detect(Array.Empty<byte>()));
            Assert.Null(ImageFormats.Detect(new byte[] { 0x89, 0x50 }));
        }
    }

    public class ImageCacheTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), $"oop-images-{Guid.NewGuid():N}");
        private readonly FakeImageSource _source = new FakeImageSource();

        public void Dispose()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        private ImageCache NewCache(TimeSpan? maxAge = null, Func<DateTime>? now = null) =>
            new ImageCache(_folder, _source, maxAge, now);

        // ---------- download and cache ----------

        [Fact]
        public async Task TheFirstRequest_DownloadsTheFileAndStoresIt()
        {
            // Arrange
            var cache = NewCache();

            // Act
            var image = await cache.GetAsync("albedo-portrait", "https://example.com/a");

            // Assert
            Assert.False(image.FromCache);
            Assert.False(image.Stale);
            Assert.Equal(PngBytes.Pixel.Length, image.Size);
            Assert.Equal(Path.Combine(cache.Folder, "albedo-portrait.png"), image.FilePath);
            Assert.Equal(PngBytes.Pixel, File.ReadAllBytes(image.FilePath));
            Assert.Equal(new[] { "https://example.com/a" }, _source.Urls);
        }

        [Fact]
        public async Task TheSecondRequest_ReadsTheFileAndAsksNobody()
        {
            // Arrange
            var cache = NewCache();
            var first = await cache.GetAsync("albedo-portrait", "https://example.com/a");

            // Act
            var second = await cache.GetAsync("albedo-portrait", "https://example.com/a");

            // Assert
            Assert.True(second.FromCache);
            Assert.Equal(first.FilePath, second.FilePath);
            Assert.Equal(1, _source.Calls);
        }

        [Fact]
        public async Task DifferentKeys_AreDifferentFiles()
        {
            // Arrange
            var cache = NewCache();

            // Act
            var a = await cache.GetAsync("a", "https://example.com/a");
            var b = await cache.GetAsync("b", "https://example.com/b");

            // Assert
            Assert.NotEqual(a.FilePath, b.FilePath);
            Assert.Equal(2, _source.Calls);
        }

        [Fact]
        public async Task TheKey_IsMadeSafeForAFileName()
        {
            // Arrange
            var cache = NewCache();

            // Act
            var image = await cache.GetAsync("Characters/Albedo?Card", "https://example.com/a");

            // Assert
            Assert.Equal("characters-albedo-card.png", Path.GetFileName(image.FilePath));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("///")]
        public async Task AnEmptyKey_IsRejected(string key)
        {
            // Arrange
            var cache = NewCache();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => cache.GetAsync(key, "https://example.com/a"));
        }

        [Fact]
        public async Task TheFormatOfTheFile_IsTheRealFormatOfThePicture()
        {
            // Arrange
            var cache = NewCache();
            _source.Data = PngBytes.Jpeg;

            // Act
            var jpeg = await cache.GetAsync("a", "u");
            _source.Data = PngBytes.Webp;
            var webp = await cache.GetAsync("b", "u");
            _source.Data = PngBytes.Gif;
            var gif = await cache.GetAsync("c", "u");

            // Assert
            Assert.EndsWith(".jpg", jpeg.FilePath);
            Assert.EndsWith(".webp", webp.FilePath);
            Assert.EndsWith(".gif", gif.FilePath);
        }

        [Fact]
        public async Task SomethingThatIsNotAPicture_IsRejectedAndNothingIsStored()
        {
            // Arrange
            var cache = NewCache();
            _source.Data = PngBytes.Html;

            // Act & Assert
            await Assert.ThrowsAsync<InvalidDataException>(() => cache.GetAsync("a", "u"));
            Assert.Empty(cache.List());
            Assert.False(Directory.Exists(_folder) && Directory.GetFiles(_folder).Length > 0);
        }

        [Fact]
        public async Task WhenTheDownloadFails_AndThereIsNoFile_TheErrorIsThrown()
        {
            // Arrange
            var cache = NewCache();
            _source.Failure = new RemoteApiException("The service is down.");

            // Act
            var exception = await Assert.ThrowsAsync<RemoteApiException>(() => cache.GetAsync("a", "u"));

            // Assert
            Assert.Equal("The service is down.", exception.Message);
        }

        [Fact]
        public async Task NoTemporaryFilesRemain()
        {
            // Arrange
            var cache = NewCache();

            // Act
            await cache.GetAsync("a", "u");

            // Assert
            Assert.DoesNotContain(Directory.GetFiles(_folder), f => f.EndsWith(".tmp"));
        }

        // ---------- expiry ----------

        [Fact]
        public async Task AnExpiredFile_IsDownloadedAgain()
        {
            // Arrange
            var now = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
            var cache = NewCache(TimeSpan.FromDays(1), () => now);
            var first = await cache.GetAsync("a", "u");
            File.SetLastWriteTimeUtc(first.FilePath, now.AddDays(-2));

            // Act
            var second = await cache.GetAsync("a", "u");

            // Assert
            Assert.False(second.FromCache);
            Assert.Equal(2, _source.Calls);
        }

        [Fact]
        public async Task AFreshFile_IsUsed()
        {
            // Arrange
            var now = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
            var cache = NewCache(TimeSpan.FromDays(1), () => now);
            var first = await cache.GetAsync("a", "u");
            File.SetLastWriteTimeUtc(first.FilePath, now.AddHours(-12));

            // Act
            var second = await cache.GetAsync("a", "u");

            // Assert
            Assert.True(second.FromCache);
            Assert.Equal(1, _source.Calls);
        }

        [Fact]
        public async Task AnExpiredFile_IsStillUsedWhenTheServiceDoesNotAnswer()
        {
            // Arrange
            var now = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
            var cache = NewCache(TimeSpan.FromDays(1), () => now);
            var first = await cache.GetAsync("a", "u");
            File.SetLastWriteTimeUtc(first.FilePath, now.AddDays(-2));
            _source.Failure = new RemoteApiException("down");

            // Act
            var second = await cache.GetAsync("a", "u");

            // Assert
            Assert.True(second.FromCache);
            Assert.True(second.Stale);
            Assert.Equal(first.FilePath, second.FilePath);
        }

        [Fact]
        public async Task ANewFormat_ReplacesTheOldFileOfTheSameKey()
        {
            // Arrange
            var now = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
            var cache = NewCache(TimeSpan.FromDays(1), () => now);
            var first = await cache.GetAsync("a", "u");
            File.SetLastWriteTimeUtc(first.FilePath, now.AddDays(-2));
            _source.Data = PngBytes.Jpeg;

            // Act
            var second = await cache.GetAsync("a", "u");

            // Assert
            Assert.EndsWith(".jpg", second.FilePath);
            Assert.False(File.Exists(first.FilePath));
            Assert.Single(cache.List());
        }

        [Fact]
        public async Task TryGetCached_KnowsFreshFilesOnly()
        {
            // Arrange
            var now = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
            var cache = NewCache(TimeSpan.FromDays(1), () => now);
            Assert.False(cache.TryGetCached("a", out _));
            var image = await cache.GetAsync("a", "u");

            // Act & Assert
            Assert.True(cache.TryGetCached("a", out var path));
            Assert.Equal(image.FilePath, path);

            File.SetLastWriteTimeUtc(image.FilePath, now.AddDays(-2));
            Assert.False(cache.TryGetCached("a", out _));
        }

        // ---------- management ----------

        [Fact]
        public async Task List_TotalBytes_AndClear()
        {
            // Arrange
            var cache = NewCache();
            await cache.GetAsync("b", "u");
            await cache.GetAsync("a", "u");

            // Act & Assert
            Assert.Equal(new[] { "a", "b" }, cache.List().Select(i => i.Key));
            Assert.Equal(2 * PngBytes.Pixel.Length, cache.TotalBytes);

            Assert.Equal(2, cache.Clear());
            Assert.Empty(cache.List());
            Assert.Equal(0, cache.TotalBytes);
        }

        [Fact]
        public void AnEmptyCache_ListsNothing()
        {
            // Arrange
            var cache = NewCache();

            // Act & Assert
            Assert.Empty(cache.List());
            Assert.Equal(0, cache.Clear());
        }

        [Fact]
        public void Constructor_RejectsBadArguments()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new ImageCache(" ", _source));
            Assert.Throws<ArgumentNullException>(() => new ImageCache(_folder, null!));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ImageCache(_folder, _source, TimeSpan.Zero));
        }
    }

    public class HttpImageSourceTests
    {
        private static HttpImageSource Source(StubHttpMessageHandler handler) => new HttpImageSource(new HttpClient(handler));

        private static StubHttpMessageHandler Picture(byte[] bytes) =>
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes) { Headers = { ContentType = new MediaTypeHeaderValue("image/png") } }
            });

        [Fact]
        public async Task DownloadAsync_ReturnsTheBytesAndTheMediaType()
        {
            // Arrange
            var handler = Picture(PngBytes.Pixel);

            // Act
            var image = await Source(handler).DownloadAsync("https://example.com/a.png");

            // Assert
            Assert.Equal(PngBytes.Pixel, image.Data);
            Assert.Equal("image/png", image.ContentType);
            Assert.Equal("https://example.com/a.png", Assert.Single(handler.RequestedUrls));
        }

        [Fact]
        public async Task NotFound_IsReported()
        {
            // Arrange
            var source = Source(StubHttpMessageHandler.Status(HttpStatusCode.NotFound));

            // Act
            var exception = await Assert.ThrowsAsync<RemoteApiException>(() => source.DownloadAsync("https://example.com/a"));

            // Assert
            Assert.Contains("not found", exception.Message);
        }

        [Fact]
        public async Task AServerError_IsReportedWithItsCode()
        {
            // Arrange
            var source = Source(StubHttpMessageHandler.Status(HttpStatusCode.InternalServerError));

            // Act
            var exception = await Assert.ThrowsAsync<RemoteApiException>(() => source.DownloadAsync("https://example.com/a"));

            // Assert
            Assert.Contains("500", exception.Message);
        }

        [Fact]
        public async Task ANetworkFailure_IsReported()
        {
            // Arrange
            var source = Source(StubHttpMessageHandler.Throwing(new HttpRequestException("no route")));

            // Act
            var exception = await Assert.ThrowsAsync<RemoteApiException>(() => source.DownloadAsync("https://example.com/a"));

            // Assert
            Assert.Contains("Cannot reach", exception.Message);
        }

        [Fact]
        public async Task ATimeout_IsReported()
        {
            // Arrange
            var source = Source(StubHttpMessageHandler.Throwing(new TaskCanceledException()));

            // Act
            var exception = await Assert.ThrowsAsync<RemoteApiException>(() => source.DownloadAsync("https://example.com/a"));

            // Assert
            Assert.Contains("timed out", exception.Message);
        }

        [Fact]
        public async Task ATooLargePicture_IsRefused()
        {
            // Arrange
            var source = Source(Picture(new byte[10 * 1024 * 1024 + 1]));

            // Act
            var exception = await Assert.ThrowsAsync<RemoteApiException>(() => source.DownloadAsync("https://example.com/a"));

            // Assert
            Assert.Contains("too large", exception.Message);
        }

        [Fact]
        public void Constructor_NullClient_Throws()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new HttpImageSource(null!));
        }

        [Fact]
        public async Task TheOfflineSource_GivesARealPng()
        {
            // Act
            var image = await new OfflineImageSource().DownloadAsync("https://anything");

            // Assert
            Assert.Equal(".png", ImageFormats.Detect(image.Data));
            Assert.Equal("image/png", image.ContentType);
        }
    }
}