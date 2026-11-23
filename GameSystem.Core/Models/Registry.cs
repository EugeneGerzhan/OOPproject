using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace GameSystem.Core.Models
{
    /// <summary>Stores objects under unique integer ids and finds them by id or by name.</summary>
    public class Registry<T> where T : class
    {
        private readonly SortedDictionary<int, T> _entries = new SortedDictionary<int, T>();
        private readonly Func<T, string> _nameSelector;

        public Registry(Func<T, string> nameSelector)
        {
            _nameSelector = nameSelector;
        }

        public IEnumerable<KeyValuePair<int, T>> Entries => _entries;
        public int Count => _entries.Count;

        public bool Contains(int id) => _entries.ContainsKey(id);

        public bool IsFree(int id) => id > 0 && !_entries.ContainsKey(id);

        public bool NameExists(string name)
        {
            return _entries.Values.Any(v => string.Equals(_nameSelector(v), name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Adds an object. Uses the requested id if it is free, otherwise throws; without a request uses the lowest free id.</summary>
        public int Add(T item, int? requestedId = null)
        {
            int id;
            if (requestedId.HasValue)
            {
                if (requestedId.Value < 1)
                {
                    throw new ArgumentException("Id must be a positive number.");
                }
                if (_entries.ContainsKey(requestedId.Value))
                {
                    throw new ArgumentException($"Id {requestedId.Value} is already taken.");
                }
                id = requestedId.Value;
            }
            else
            {
                id = 1;
                while (_entries.ContainsKey(id))
                {
                    id++;
                }
            }

            _entries[id] = item;
            return id;
        }

        public int? IdOf(T item)
        {
            foreach (var entry in _entries)
            {
                if (ReferenceEquals(entry.Value, item))
                {
                    return entry.Key;
                }
            }
            return null;
        }

        public bool TryGet(int id, [NotNullWhen(true)] out T? item)
        {
            return _entries.TryGetValue(id, out item);
        }

        /// <summary>Finds an object by id (if the key is a number) or by name.</summary>
        public bool TryFind(string key, [NotNullWhen(true)] out T? item, out string error)
        {
            item = null;
            error = string.Empty;
            key = (key ?? string.Empty).Trim();

            if (key.Length == 0)
            {
                error = "id or name is empty.";
                return false;
            }

            if (int.TryParse(key, out int id) && _entries.TryGetValue(id, out var byId))
            {
                item = byId;
                return true;
            }

            var matches = _entries.Values
                .Where(v => string.Equals(_nameSelector(v), key, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
            {
                error = $"'{key}' not found.";
                return false;
            }
            if (matches.Count > 1)
            {
                error = $"several objects are named '{key}', use the id instead (see 'ls').";
                return false;
            }

            item = matches[0];
            return true;
        }
    }
}
