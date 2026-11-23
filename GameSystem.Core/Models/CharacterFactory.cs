using System;
using System.Collections.Generic;
using System.Linq;

namespace GameSystem.Core.Models
{
    /// <summary>
    /// The only place that knows all character classes. To add a class, add one entry to Entries.
    /// </summary>
    public static class CharacterFactory
    {
        private sealed class Entry
        {
            public Entry(string key, Type type, Func<string, Character> create)
            {
                Key = key;
                Type = type;
                Create = create;
            }

            public string Key { get; }
            public Type Type { get; }
            public Func<string, Character> Create { get; }
        }

        private static readonly List<Entry> Entries = new List<Entry>
        {
            new Entry("barbarian", typeof(HalfOrcBarbarian), name => new HalfOrcBarbarian(name)),
            new Entry("monk", typeof(Monk), name => new Monk(name))
        };

        public static IReadOnlyList<string> ClassKeys => Entries.Select(e => e.Key).ToList();

        public static Character Create(string classKey, string name)
        {
            var entry = Entries.FirstOrDefault(e => string.Equals(e.Key, classKey, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                throw new ArgumentException($"Unknown character class '{classKey}'.");
            }
            return entry.Create(name);
        }

        public static string KeyOf(Character character)
        {
            var entry = Entries.FirstOrDefault(e => e.Type == character.GetType());
            if (entry == null)
            {
                throw new ArgumentException($"Character class '{character.GetType().Name}' is not registered.");
            }
            return entry.Key;
        }
    }
}
