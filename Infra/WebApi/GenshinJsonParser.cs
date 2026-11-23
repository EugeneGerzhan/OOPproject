using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Infra.WebApi
{
    /// <summary>Tolerant parser: every field except the name is optional.</summary>
    public static class GenshinJsonParser
    {
        public static IReadOnlyList<string> ParseIdList(string json)
        {
            using var document = Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new RemoteApiException("Unexpected response: a list of ids was expected.");
            }

            var ids = new List<string>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.String) continue;

                string? id = element.GetString();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    ids.Add(id);
                }
            }
            return ids;
        }

        public static RemoteCharacter ParseCharacter(string id, string json)
        {
            using var document = Parse(json);
            var root = RequireObject(document.RootElement);

            string name = GetString(root, "name").Trim();
            if (name.Length == 0)
            {
                throw new RemoteApiException("Unexpected response: the character has no name.");
            }

            return new RemoteCharacter(
                id, name,
                GetString(root, "title"),
                GetString(root, "vision"),
                GetString(root, "weapon"),
                GetString(root, "nation"),
                GetString(root, "affiliation"),
                GetInt(root, "rarity"),
                GetString(root, "description"),
                GetTalents(root, "skillTalents"),
                GetTalents(root, "passiveTalents"),
                GetString(root, "image"));
        }

        public static RemoteWeapon ParseWeapon(string id, string json)
        {
            using var document = Parse(json);
            var root = RequireObject(document.RootElement);

            string name = GetString(root, "name").Trim();
            if (name.Length == 0)
            {
                throw new RemoteApiException("Unexpected response: the weapon has no name.");
            }

            return new RemoteWeapon(
                id, name,
                GetString(root, "type"),
                GetInt(root, "rarity"),
                GetInt(root, "baseAttack"),
                GetString(root, "subStat"),
                GetString(root, "passiveName"),
                GetString(root, "passiveDesc"),
                GetString(root, "location"));
        }

        // ---------- helpers ----------

        private static JsonDocument Parse(string json)
        {
            try
            {
                return JsonDocument.Parse(json);
            }
            catch (JsonException ex)
            {
                throw new RemoteApiException("The remote service returned invalid JSON.", ex);
            }
        }

        private static JsonElement RequireObject(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new RemoteApiException("Unexpected response: a JSON object was expected.");
            }
            return element;
        }

        private static string GetString(JsonElement element, string name)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? string.Empty;
            }
            return string.Empty;
        }

        private static int GetInt(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                return 0;
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                return value.TryGetInt32(out int number) ? number : (int)Math.Round(value.GetDouble());
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out int parsed))
            {
                return parsed;
            }

            return 0;
        }

        private static IReadOnlyList<RemoteTalent> GetTalents(JsonElement element, string name)
        {
            var talents = new List<RemoteTalent>();
            if (!element.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
            {
                return talents;
            }

            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;

                string talentName = GetString(item, "name").Trim();
                if (talentName.Length == 0) continue;

                talents.Add(new RemoteTalent(talentName, GetString(item, "unlock"), GetString(item, "description")));
            }
            return talents;
        }
    }
}
