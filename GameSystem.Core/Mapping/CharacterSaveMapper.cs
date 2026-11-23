using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameSystem.Core.Abilities;
using GameSystem.Core.Models;
using GameSystem.Core.Remote;
using Infra.Data;

namespace GameSystem.Core.Mapping
{
    /// <summary>Converts between GameState (domain objects) and CharacterSave (data models).</summary>
    public static class CharacterSaveMapper
    {
        private const string RageType = "rage";
        private const string FlurryType = "flurry";
        private const string CustomType = "custom";
        private const string RemoteClassKey = "genshin";

        public static CharacterSave ToSave(string saveName, GameState state)
        {
            var save = new CharacterSave { SaveName = saveName, LastModified = DateTime.Now };

            foreach (var entry in state.Items.Entries)
            {
                var item = entry.Value;
                save.Items.Add(new ItemData(entry.Key, item.Name, item.HealthBonus, item.ArmorBonus, item.AttackBonus, item.Description));
            }

            foreach (var entry in state.Abilities.Entries)
            {
                save.Abilities.Add(ToData(entry.Key, entry.Value));
            }

            int itemLinkId = 1;
            int abilityLinkId = 1;

            foreach (var entry in state.Characters.Entries)
            {
                var c = entry.Value;
                int uniqueAbilityId = state.Abilities.IdOf(c.UniqueAbility) ?? 0;

                string classKey = c is ApiCharacter ? RemoteClassKey : CharacterFactory.KeyOf(c);
                ProfileData? profile = c is ApiCharacter api ? ProfileToData(api.Profile) : null;

                save.Characters.Add(new CharacterData(
                    entry.Key, c.Name, classKey,
                    c.Health, c.MaxHealth, c.Armor, c.AttackPower, uniqueAbilityId, profile));

                if (c.EquippedItem != null && state.Items.IdOf(c.EquippedItem) is int itemId)
                {
                    save.ItemLinks.Add(new CharacterItems(itemLinkId++, entry.Key, itemId));
                }

                foreach (var ability in c.LearnedAbilities)
                {
                    if (state.Abilities.IdOf(ability) is int abilityId)
                    {
                        save.AbilityLinks.Add(new CharacterAbilities(abilityLinkId++, entry.Key, abilityId));
                    }
                }
            }

            return save;
        }

        /// <summary>Builds a brand new GameState; throws InvalidDataException if the save is inconsistent.</summary>
        public static GameState ToGameState(CharacterSave save)
        {
            var state = new GameState();

            foreach (var d in save.Items)
            {
                state.Items.Add(new Item(d.Name, d.HealthBonus, d.ArmorBonus, d.AttackBonus, d.Description ?? string.Empty), d.Id);
            }

            // Characters first: their unique abilities come from the character itself.
            var characters = new Dictionary<int, Character>();
            foreach (var d in save.Characters)
            {
                Character character;
                if (d.Profile != null)
                {
                    // Remote character: its unique ability is described by an AbilityData entry.
                    var uniqueData = save.Abilities.FirstOrDefault(a => a.Id == d.UniqueAbilityId);
                    if (uniqueData == null)
                    {
                        throw new InvalidDataException($"The unique ability of character '{d.Name}' is missing in the save.");
                    }

                    character = new ApiCharacter(
                        d.Name,
                        DataToProfile(d.Profile),
                        new CharacterStats(d.MaxHealth, d.Armor, d.AttackPower),
                        CreateAbility(uniqueData));
                }
                else
                {
                    character = CharacterFactory.Create(d.Class, d.Name);
                }

                state.Characters.Add(character, d.Id);
                characters[d.Id] = character;

                if (d.UniqueAbilityId > 0)
                {
                    state.Abilities.Add(character.UniqueAbility, d.UniqueAbilityId);
                }
            }

            // Remaining (custom / standalone) abilities.
            foreach (var d in save.Abilities)
            {
                if (state.Abilities.Contains(d.Id))
                {
                    continue; // already registered as a unique ability of a character
                }
                state.Abilities.Add(CreateAbility(d), d.Id);
            }

            // Relations and current stats.
            foreach (var d in save.Characters)
            {
                var character = characters[d.Id];

                Item? equipped = null;
                var itemLink = save.ItemLinks.FirstOrDefault(l => l.CharacterId == d.Id);
                if (itemLink != null && !state.Items.TryGet(itemLink.ItemId, out equipped))
                {
                    throw new InvalidDataException($"Item {itemLink.ItemId} of character '{d.Name}' is missing in the save.");
                }

                var learned = new List<IAbility>();
                foreach (var link in save.AbilityLinks.Where(l => l.CharacterId == d.Id))
                {
                    if (!state.Abilities.TryGet(link.AbilityId, out var ability))
                    {
                        throw new InvalidDataException($"Ability {link.AbilityId} of character '{d.Name}' is missing in the save.");
                    }
                    learned.Add(ability);
                }

                character.RestoreState(d.Health, d.MaxHealth, d.Armor, d.AttackPower, equipped, learned);
            }

            return state;
        }

        private static AbilityData ToData(int id, IAbility ability)
        {
            switch (ability)
            {
                case RageAbility _:
                    return new AbilityData(id, RageType, ability.Name, 0, 0);
                case FlurryOfBlowsAbility _:
                    return new AbilityData(id, FlurryType, ability.Name, 0, 0);
                case CustomAbility custom:
                    return new AbilityData(id, CustomType, custom.Name, custom.Hits, custom.AttackBonus, custom.Description);
                default:
                    throw new NotSupportedException($"Ability type '{ability.GetType().Name}' cannot be saved.");
            }
        }

        private static IAbility CreateAbility(AbilityData d)
        {
            switch (d.Type)
            {
                case RageType:
                    return new RageAbility();
                case FlurryType:
                    return new FlurryOfBlowsAbility();
                case CustomType:
                    return new CustomAbility(d.Name, d.Hits, d.AttackBonus, d.Description ?? string.Empty);
                default:
                    throw new InvalidDataException($"Unknown ability type '{d.Type}'.");
            }
        }

        private static ProfileData ProfileToData(CharacterProfile p) =>
            new ProfileData(p.RemoteId, p.Title, p.Vision, p.WeaponType, p.Nation, p.Affiliation, p.Description, p.Rarity);

        private static CharacterProfile DataToProfile(ProfileData p) =>
            new CharacterProfile(
                p.RemoteId ?? string.Empty, p.Title ?? string.Empty, p.Vision ?? string.Empty,
                p.WeaponType ?? string.Empty, p.Nation ?? string.Empty, p.Affiliation ?? string.Empty,
                p.Description ?? string.Empty, p.Rarity);
    }
}