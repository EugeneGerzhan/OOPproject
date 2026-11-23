using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Infra.Data;
using Moq;
using ProjectTests.Mocks;
using Xunit;

namespace ProjectTests.InfraTests
{
    public class DataModelTests
    {
        [Fact]
        public void Saves_StartWithEmptyNonNullCollections()
        {
            // Arrange & Act
            var characterSave = new CharacterSave();
            var repository = new Repository();

            // Assert
            Assert.Empty(characterSave.Characters);
            Assert.Empty(characterSave.Abilities);
            Assert.Empty(characterSave.Items);
            Assert.Empty(characterSave.ItemLinks);
            Assert.Empty(characterSave.AbilityLinks);
            Assert.Empty(repository.CharacterSaves);
            Assert.Empty(repository.TextSaves);
            Assert.Null(new TextSave().TextRoot);
        }
    }

    public class JsonRepositoryStorageTests : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"oop-tests-{Guid.NewGuid():N}.json");

        public void Dispose()
        {
            if (File.Exists(_path))
                File.Delete(_path);
        }

        private static Repository BuildRepository()
        {
            var repository = new Repository();

            repository.TextSaves["doc"] = new TextSave
            {
                SaveName = "doc",
                TextRoot = new TextNodeData(0, TextNodeKinds.Root, "Привіт", new List<TextNodeData>
                {
                    new TextNodeData(1, TextNodeKinds.Heading, "Розділ", new List<TextNodeData>
                    {
                        new TextNodeData(2, TextNodeKinds.Paragraph, "Текст")
                    })
                })
            };

            var arena = new CharacterSave { SaveName = "arena" };
            arena.Characters.Add(new CharacterData(1, "Growler", "barbarian", 120, 120, 5, 20, 1));
            arena.Items.Add(new ItemData(1, "Axe", 0, 0, 5));
            arena.Abilities.Add(new AbilityData(1, "rage", "Rage", 0, 0));
            arena.Abilities.Add(new AbilityData(2, "custom", "Whirlwind", 3, 2));
            arena.ItemLinks.Add(new CharacterItems(1, 1, 1));
            arena.AbilityLinks.Add(new CharacterAbilities(1, 1, 2));
            repository.CharacterSaves["arena"] = arena;

            return repository;
        }

        [Fact]
        public void Location_IsTheFullPath()
        {
            // Arrange & Act
            var storage = new JsonRepositoryStorage(_path);

            // Assert
            Assert.Equal(Path.GetFullPath(_path), storage.Location);
        }

        [Fact]
        public void Load_MissingFile_GivesEmptyRepository()
        {
            // Arrange
            var storage = new JsonRepositoryStorage(_path);

            // Act
            var repository = storage.Load();

            // Assert
            Assert.Empty(repository.TextSaves);
            Assert.Empty(repository.CharacterSaves);
        }

        [Fact]
        public void Load_WhitespaceFile_GivesEmptyRepository()
        {
            // Arrange
            File.WriteAllText(_path, "   ");
            var storage = new JsonRepositoryStorage(_path);

            // Act
            var repository = storage.Load();

            // Assert
            Assert.Empty(repository.TextSaves);
        }

        [Fact]
        public void Load_CorruptedJson_Throws()
        {
            // Arrange
            File.WriteAllText(_path, "{ this is not json");
            var storage = new JsonRepositoryStorage(_path);

            // Act & Assert
            Assert.ThrowsAny<JsonException>(() => storage.Load());
        }

        [Fact]
        public void SaveThenLoad_RoundTripsTextSaves()
        {
            // Arrange
            var storage = new JsonRepositoryStorage(_path);

            // Act
            storage.Save(BuildRepository());
            var loaded = storage.Load();

            // Assert
            var root = loaded.TextSaves["doc"].TextRoot!;
            Assert.Equal("Привіт", root.Text);
            Assert.Equal("Розділ", root.Children![0].Text);
            Assert.Equal("Текст", root.Children[0].Children![0].Text);
            Assert.Equal(TextNodeKinds.Paragraph, root.Children[0].Children![0].Kind);
        }

        [Fact]
        public void SaveThenLoad_RoundTripsCharacterSaves()
        {
            // Arrange
            var storage = new JsonRepositoryStorage(_path);

            // Act
            storage.Save(BuildRepository());
            var arena = storage.Load().CharacterSaves["arena"];

            // Assert
            Assert.Equal("Growler", arena.Characters[0].Name);
            Assert.Equal(120, arena.Characters[0].Health);
            Assert.Equal("Axe", arena.Items[0].Name);
            Assert.Equal(2, arena.Abilities.Count);
            Assert.Equal(3, arena.Abilities[1].Hits);
            Assert.Equal(1, arena.ItemLinks[0].ItemId);
            Assert.Equal(2, arena.AbilityLinks[0].AbilityId);
        }

        [Fact]
        public void Save_KeepsUkrainianTextReadableInFile()
        {
            // Arrange
            var storage = new JsonRepositoryStorage(_path);

            // Act
            storage.Save(BuildRepository());
            string json = File.ReadAllText(_path);

            // Assert
            Assert.Contains("Привіт", json);
        }

        [Fact]
        public void Save_StampsLastModified()
        {
            // Arrange
            var storage = new JsonRepositoryStorage(_path);

            // Act
            storage.Save(new Repository());
            var loaded = storage.Load();

            // Assert
            Assert.True(loaded.LastModified > DateTime.Now.AddMinutes(-1));
        }
    }

    public class SaveManagerTests
    {
        private static TextSave NewTextSave(string name) => new TextSave { SaveName = name };

        [Fact]
        public void SaveText_PutsSaveIntoRepositoryAndWritesStorageOnce()
        {
            // Arrange
            var repository = new Repository();
            var storage = new Mock<IRepositoryStorage>();
            storage.Setup(s => s.Load()).Returns(repository);
            var manager = new SaveManager(storage.Object);

            // Act
            manager.SaveText(NewTextSave("doc"));

            // Assert
            Assert.True(repository.TextSaves.ContainsKey("doc"));
            storage.Verify(s => s.Save(repository), Times.Once());
        }

        [Fact]
        public void Location_ComesFromStorage()
        {
            // Arrange
            var manager = new SaveManager(new InMemoryStorage());

            // Act & Assert
            Assert.Equal("memory", manager.Location);
        }

        [Fact]
        public void SaveText_SameNameDifferentCase_ReplacesTheOldSave()
        {
            // Arrange
            var storage = new InMemoryStorage();
            var manager = new SaveManager(storage);
            manager.SaveText(NewTextSave("Doc"));

            // Act
            manager.SaveText(NewTextSave("doc"));

            // Assert
            var info = Assert.Single(manager.ListTextSaves());
            Assert.Equal("doc", info.Name);
            Assert.Equal(2, storage.SaveCalls);
        }

        [Fact]
        public void SaveText_StampsLastModified()
        {
            // Arrange
            var manager = new SaveManager(new InMemoryStorage());
            var save = NewTextSave("doc");

            // Act
            manager.SaveText(save);

            // Assert
            Assert.True(save.LastModified > DateTime.Now.AddMinutes(-1));
        }

        [Fact]
        public void TryLoadText_IsCaseInsensitive()
        {
            // Arrange
            var manager = new SaveManager(new InMemoryStorage());
            manager.SaveText(NewTextSave("Doc"));

            // Act
            bool found = manager.TryLoadText("DOC", out var save);

            // Assert
            Assert.True(found);
            Assert.Equal("Doc", save!.SaveName);
        }

        [Fact]
        public void TryLoadText_Missing_ReturnsFalse()
        {
            // Arrange
            var manager = new SaveManager(new InMemoryStorage());

            // Act
            bool found = manager.TryLoadText("nope", out var save);

            // Assert
            Assert.False(found);
            Assert.Null(save);
        }

        [Fact]
        public void TryLoadCharacters_ReturnsSavedState()
        {
            // Arrange
            var manager = new SaveManager(new InMemoryStorage());
            manager.SaveCharacters(new CharacterSave { SaveName = "arena" });

            // Act
            bool found = manager.TryLoadCharacters("arena", out var save);

            // Assert
            Assert.True(found);
            Assert.Equal("arena", save!.SaveName);
        }

        [Fact]
        public void TextAndCharacterSaves_AreSeparate()
        {
            // Arrange
            var manager = new SaveManager(new InMemoryStorage());

            // Act
            manager.SaveText(NewTextSave("same"));

            // Assert
            Assert.Single(manager.ListTextSaves());
            Assert.Empty(manager.ListCharacterSaves());
            Assert.False(manager.TryLoadCharacters("same", out _));
        }

        [Fact]
        public void ListSaves_AreSortedByNameIgnoringCase()
        {
            // Arrange
            var manager = new SaveManager(new InMemoryStorage());
            manager.SaveText(NewTextSave("b"));
            manager.SaveText(NewTextSave("A"));
            manager.SaveText(NewTextSave("c"));

            // Act
            var names = manager.ListTextSaves().Select(i => i.Name).ToList();

            // Assert
            Assert.Equal(new[] { "A", "b", "c" }, names);
        }
    }
}
