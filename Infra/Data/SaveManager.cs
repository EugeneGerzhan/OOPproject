using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Infra.Data
{
    public record SaveInfo(string Name, DateTime LastModified);

    /// <summary>Named saves on top of any IRepositoryStorage. Names are case-insensitive.</summary>
    public class SaveManager
    {
        private readonly IRepositoryStorage _storage;

        public SaveManager(IRepositoryStorage storage)
        {
            _storage = storage;
        }

        public string Location => _storage.Location;

        public void SaveText(TextSave save)
        {
            var repository = _storage.Load();
            Put(repository.TextSaves, save);
            _storage.Save(repository);
        }

        public void SaveCharacters(CharacterSave save)
        {
            var repository = _storage.Load();
            Put(repository.CharacterSaves, save);
            repository.LastCharacterSave = save.SaveName;
            _storage.Save(repository);
        }
       
        /// <summary>
        /// The name of the character save that was written last (an autosave counts too);
        /// null when there is none or when that save does not exist any more.
        /// </summary>
        public string? LastCharacterSaveName
        {
            get
            {
                var repository = _storage.Load();
                string? name = repository.LastCharacterSave;
                if (name == null)
                {
                    return null;
                }
                return repository.CharacterSaves.Keys.FirstOrDefault(
                    key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));
            }
        }

        public bool TryLoadText(string name, [NotNullWhen(true)] out TextSave? save)
            => TryGet(_storage.Load().TextSaves, name, out save);

        public bool TryLoadCharacters(string name, [NotNullWhen(true)] out CharacterSave? save)
            => TryGet(_storage.Load().CharacterSaves, name, out save);

        public IReadOnlyList<SaveInfo> ListTextSaves() => Describe(_storage.Load().TextSaves);

        public IReadOnlyList<SaveInfo> ListCharacterSaves() => Describe(_storage.Load().CharacterSaves);

        private static void Put<T>(Dictionary<string, T> saves, T save) where T : SaveBase
        {
            save.LastModified = DateTime.Now;

            var existingKey = saves.Keys.FirstOrDefault(k => string.Equals(k, save.SaveName, StringComparison.OrdinalIgnoreCase));
            if (existingKey != null)
            {
                saves.Remove(existingKey);
            }
            saves[save.SaveName] = save;
        }

        private static bool TryGet<T>(Dictionary<string, T> saves, string name, [NotNullWhen(true)] out T? save) where T : SaveBase
        {
            var key = saves.Keys.FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
            if (key == null)
            {
                save = null;
                return false;
            }

            save = saves[key];
            return true;
        }

        private static IReadOnlyList<SaveInfo> Describe<T>(Dictionary<string, T> saves) where T : SaveBase
        {
            return saves.Values
                .OrderBy(s => s.SaveName, StringComparer.OrdinalIgnoreCase)
                .Select(s => new SaveInfo(s.SaveName, s.LastModified))
                .ToList();
        }
        /// <summary>Appends the result of a finished battle to the repository.</summary>
        public void AddBattleResult(BattleResultData result)
        {
            var repository = _storage.Load();
            repository.BattleResults.Add(result);
            _storage.Save(repository);
        }

        /// <summary>All saved results, the oldest first.</summary>
        public IReadOnlyList<BattleResultData> ListBattleResults() => _storage.Load().BattleResults.ToList();
    }
}