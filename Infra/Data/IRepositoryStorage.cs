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

        public Repository Load()
        {
            if (!File.Exists(_path))
            {
                return new Repository();
            }

            string json = File.ReadAllText(_path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Repository();
            }

            return JsonSerializer.Deserialize<Repository>(json, Options) ?? new Repository();
        }

        public void Save(Repository repository)
        {
            repository.LastModified = DateTime.Now;
            File.WriteAllText(_path, JsonSerializer.Serialize(repository, Options));
        }
    }
}