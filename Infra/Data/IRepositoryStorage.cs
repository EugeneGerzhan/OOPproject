using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Infra.Data
{
    /// <summary>Where the repository lives (file, database, memory...). Replaceable.</summary>
    public interface IRepositoryStorage
    {
        string Location { get; }
        Repository Load();
        void Save(Repository repository);
    }

    /// <summary>
    /// Keeps the repository in a JSON file.
    /// <para>
    /// A save is safe against a crash: the new content is written to a temporary file first, the previous file is
    /// copied to <c>.bak</c> and only then the temporary file takes its place. If the main file is damaged,
    /// the backup (the previous version) is read instead.
    /// </para>
    /// </summary>
    public class JsonRepositoryStorage : IRepositoryStorage
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // keeps Ukrainian text readable in the file
        };

        private readonly string _path;

        public JsonRepositoryStorage(string path)
        {
            _path = Path.GetFullPath(path);
        }

        public string Location => _path;

        private string BackupPath => _path + ".bak";

        private string TempPath => _path + ".tmp";

        public Repository Load()
        {
            if (!File.Exists(_path))
            {
                return new Repository();
            }

            try
            {
                return Read(_path);
            }
            catch (JsonException) when (File.Exists(BackupPath))
            {
                // The file is damaged: the previous version is better than nothing.
                return Read(BackupPath);
            }
        }

        public void Save(Repository repository)
        {
            repository.LastModified = DateTime.Now;
            repository.Version = RepositoryFormat.CurrentVersion;

            string? directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 1. Everything is written to a temporary file: a crash here leaves the old file untouched.
            File.WriteAllText(TempPath, JsonSerializer.Serialize(repository, Options));

            // 2. The previous version is kept as a backup, 3. one move puts the new file in its place.
            if (File.Exists(_path))
            {
                File.Copy(_path, BackupPath, overwrite: true);
            }
            File.Move(TempPath, _path, overwrite: true);
        }

        private static Repository Read(string path)
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Repository();
            }

            var repository = JsonSerializer.Deserialize<Repository>(json, Options) ?? new Repository();
            if (repository.Version > RepositoryFormat.CurrentVersion)
            {
                throw new InvalidDataException(
                    $"The file '{path}' was written by a newer version of the program " +
                    $"(format {repository.Version}, this program reads up to {RepositoryFormat.CurrentVersion}).");
            }
            return repository;
        }
    }
}