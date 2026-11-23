using System;
using System.Linq;
using GameSystem.Core.Interfaces;
using GameSystem.Core.Models;
using Moq;
using Xunit;

namespace ProjectTests.CharacterTests
{
    public class RegistryTests
    {
        private static Registry<Item> NewRegistry() => new Registry<Item>(i => i.Name);

        [Fact]
        public void Add_WithoutId_UsesLowestFreeId()
        {
            // Arrange
            var registry = NewRegistry();

            // Act
            int first = registry.Add(new Item("a"));
            int fifth = registry.Add(new Item("b"), 5);
            int second = registry.Add(new Item("c"));

            // Assert
            Assert.Equal(1, first);
            Assert.Equal(5, fifth);
            Assert.Equal(2, second);
            Assert.Equal(3, registry.Count);
        }

        [Fact]
        public void Add_TakenId_Throws()
        {
            // Arrange
            var registry = NewRegistry();
            registry.Add(new Item("a"), 3);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => registry.Add(new Item("b"), 3));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-4)]
        public void Add_NonPositiveId_Throws(int id)
        {
            // Arrange
            var registry = NewRegistry();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => registry.Add(new Item("a"), id));
        }

        [Fact]
        public void IsFree_ChecksPositiveAndNotTaken()
        {
            // Arrange
            var registry = NewRegistry();
            registry.Add(new Item("a"), 2);

            // Act & Assert
            Assert.False(registry.IsFree(0));
            Assert.False(registry.IsFree(2));
            Assert.True(registry.IsFree(3));
        }

        [Fact]
        public void Entries_AreOrderedById()
        {
            // Arrange
            var registry = NewRegistry();
            registry.Add(new Item("a"), 3);
            registry.Add(new Item("b"), 1);
            registry.Add(new Item("c"), 2);

            // Act
            var ids = registry.Entries.Select(e => e.Key).ToList();

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, ids);
        }

        [Fact]
        public void TryFind_ById()
        {
            // Arrange
            var registry = NewRegistry();
            var sword = new Item("Sword");
            registry.Add(sword);

            // Act
            bool found = registry.TryFind("1", out var item, out _);

            // Assert
            Assert.True(found);
            Assert.Same(sword, item);
        }

        [Fact]
        public void TryFind_ByName_IsCaseInsensitive()
        {
            // Arrange
            var registry = NewRegistry();
            var sword = new Item("Sword");
            registry.Add(sword);

            // Act
            bool found = registry.TryFind("sWORD", out var item, out _);

            // Assert
            Assert.True(found);
            Assert.Same(sword, item);
        }

        [Fact]
        public void TryFind_NumericKeyThatIsNotAnId_FallsBackToName()
        {
            // Arrange
            var registry = NewRegistry();
            var odd = new Item("7");
            registry.Add(odd); // id 1

            // Act
            bool found = registry.TryFind("7", out var item, out _);

            // Assert
            Assert.True(found);
            Assert.Same(odd, item);
        }

        [Fact]
        public void TryFind_Unknown_ReportsNotFound()
        {
            // Arrange
            var registry = NewRegistry();

            // Act
            bool found = registry.TryFind("ghost", out var item, out var error);

            // Assert
            Assert.False(found);
            Assert.Null(item);
            Assert.Equal("'ghost' not found.", error);
        }

        [Fact]
        public void TryFind_EmptyKey_ReportsError()
        {
            // Arrange
            var registry = NewRegistry();

            // Act
            bool found = registry.TryFind("  ", out _, out var error);

            // Assert
            Assert.False(found);
            Assert.Equal("id or name is empty.", error);
        }

        [Fact]
        public void TryFind_DuplicateNames_AsksForId()
        {
            // Arrange
            var registry = NewRegistry();
            registry.Add(new Item("dup"));
            registry.Add(new Item("dup"));

            // Act
            bool found = registry.TryFind("dup", out _, out var error);

            // Assert
            Assert.False(found);
            Assert.Contains("several", error);
        }

        [Fact]
        public void IdOf_UsesReferenceIdentity()
        {
            // Arrange
            var registry = NewRegistry();
            var stored = new Item("a");
            registry.Add(stored, 4);

            // Act & Assert
            Assert.Equal(4, registry.IdOf(stored));
            Assert.Null(registry.IdOf(new Item("a")));
        }

        [Fact]
        public void NameExists_IsCaseInsensitive()
        {
            // Arrange
            var registry = NewRegistry();
            registry.Add(new Item("Sword"));

            // Act & Assert
            Assert.True(registry.NameExists("SWORD"));
            Assert.False(registry.NameExists("Axe"));
        }

        [Fact]
        public void ContainsAndTryGet_WorkById()
        {
            // Arrange
            var registry = NewRegistry();
            var item = new Item("a");
            registry.Add(item, 2);

            // Act & Assert
            Assert.True(registry.Contains(2));
            Assert.False(registry.Contains(1));
            Assert.True(registry.TryGet(2, out var found));
            Assert.Same(item, found);
            Assert.False(registry.TryGet(1, out _));
        }
    }

    public class CharacterFactoryTests
    {
        [Fact]
        public void ClassKeys_ContainKnownClasses()
        {
            // Act
            var keys = CharacterFactory.ClassKeys;

            // Assert
            Assert.Contains("barbarian", keys);
            Assert.Contains("monk", keys);
        }

        [Fact]
        public void Create_Barbarian()
        {
            // Act
            var character = CharacterFactory.Create("barbarian", "Growler");

            // Assert
            Assert.IsType<HalfOrcBarbarian>(character);
            Assert.Equal("Growler", character.Name);
        }

        [Fact]
        public void Create_IsCaseInsensitive()
        {
            // Act
            var character = CharacterFactory.Create("MONK", "Shi Fuu");

            // Assert
            Assert.IsType<Monk>(character);
        }

        [Fact]
        public void Create_UnknownClass_Throws()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => CharacterFactory.Create("wizard", "Gandalf"));
        }

        [Fact]
        public void KeyOf_ReturnsRegisteredKey()
        {
            // Act & Assert
            Assert.Equal("monk", CharacterFactory.KeyOf(new Monk("a")));
            Assert.Equal("barbarian", CharacterFactory.KeyOf(new HalfOrcBarbarian("b")));
        }
    }

    public class GameStateTests
    {
        private readonly IGameLogger _logger = Mock.Of<IGameLogger>();

        [Fact]
        public void UsersOf_Item_ReturnsOnlyCharactersWearingIt()
        {
            // Arrange
            var state = new GameState();
            var barbarian = new HalfOrcBarbarian("Growler");
            var monk = new Monk("Shi Fuu");
            state.Characters.Add(barbarian);
            state.Characters.Add(monk);
            var axe = new Item("Axe", attackBonus: 5);
            state.Items.Add(axe);
            barbarian.Equip(axe, _logger);

            // Act
            var users = state.UsersOf(axe).ToList();

            // Assert
            Assert.Same(barbarian, Assert.Single(users));
        }

        [Fact]
        public void UsersOf_Ability_IncludesOwnersAndLearners()
        {
            // Arrange
            var state = new GameState();
            var barbarian = new HalfOrcBarbarian("Growler");
            var monk = new Monk("Shi Fuu");
            state.Characters.Add(barbarian);
            state.Characters.Add(monk);

            // Act
            var before = state.UsersOf(barbarian.UniqueAbility).ToList();
            monk.LearnAbility(barbarian.UniqueAbility, _logger);
            var after = state.UsersOf(barbarian.UniqueAbility).ToList();

            // Assert
            Assert.Single(before);
            Assert.Equal(2, after.Count);
        }
    }
}