using System.Text;
using System.Text.RegularExpressions;
using GameSystem.Core.Abilities;
using GameSystem.Core.Models;
using GameSystem.Core.Remote;
using Infra.Display;
using OOPTextProject.TextSystem;
using OOPTextProject.TextSystem.Building;
using OOPTextProject.TextSystem.Formatting;

namespace OOPproject.SOLID;

/// <summary>
/// Builds a character sheet as a structured document with <see cref="DocumentBuilder"/> (Builder)
/// and presents it in any format supported by <see cref="ITextFormatFactory"/> (Abstract Factory).
/// </summary>
public class CharacterSheetBuilder
{
    private const int WrapWidth = 68;
    private const int MaxDescription = 240;

    /// <summary>Creates the sheet of a character.</summary>
    /// <param name="character">The character.</param>
    /// <param name="formats">The format of the sheet; plain text when omitted.</param>
    public IDisplayable Build(Character character, ITextFormatFactory? formats = null)
    {
        var doc = new DocumentBuilder(character.Name);

        doc.BeginHeading("Profile");
        foreach (string line in ProfileLines(character))
        {
            AddWrapped(doc, line);
        }
        doc.EndHeading();

        doc.BeginHeading("Stats")
            .AddParagraph($"Health: {character.Health}/{character.MaxHealth}")
            .AddParagraph($"Armor: {character.Armor}")
            .AddParagraph($"Attack: {character.AttackPower}")
            .AddParagraph($"Status: {(character.IsAlive ? "alive" : "defeated")}")
            .EndHeading();

        doc.BeginHeading("Equipment");
        AddEquipment(doc, character.EquippedItem);
        doc.EndHeading();

        doc.BeginHeading("Abilities");

        doc.BeginHeading("Unique");
        AddAbility(doc, character.UniqueAbility);
        doc.EndHeading();

        doc.BeginHeading("Learned");
        if (character.LearnedAbilities.Count == 0)
        {
            doc.AddParagraph("none");
        }
        else
        {
            foreach (var ability in character.LearnedAbilities)
            {
                AddAbility(doc, ability);
            }
        }
        doc.EndHeading();

        doc.EndHeading();

        return new TextDto(doc.Build(), false, formats);
    }

    /// <summary>Splits text into lines of at most <paramref name="width"/> characters (a longer word stays alone).</summary>
    public static IReadOnlyList<string> Wrap(string? text, int width)
    {
        var lines = new List<string>();
        var line = new StringBuilder();

        foreach (string word in (text ?? string.Empty).Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                lines.Add(line.ToString());
                line.Clear();
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }
            line.Append(word);
        }

        if (line.Length > 0)
        {
            lines.Add(line.ToString());
        }
        return lines;
    }

    /// <summary>Collapses whitespace and cuts the text to <paramref name="maxLength"/> characters, adding "...".</summary>
    public static string Shorten(string? text, int maxLength)
    {
        string clean = Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();
        if (clean.Length <= maxLength)
        {
            return clean;
        }
        return clean.Substring(0, Math.Max(0, maxLength - 3)).TrimEnd() + "...";
    }

    // ---------- helpers ----------

    private static IEnumerable<string> ProfileLines(Character character)
    {
        if (character is not ApiCharacter api)
        {
            yield return $"Class: {character.GetType().Name}";
            yield break;
        }

        var p = api.Profile;
        yield return $"Source: remote database ({p.RemoteId})";
        if (!string.IsNullOrWhiteSpace(p.Title)) yield return $"Title: {p.Title}";
        yield return $"Vision: {OrUnknown(p.Vision)} | Weapon type: {OrUnknown(p.WeaponType)} | Rarity: {p.Rarity}";

        var origin = new List<string>();
        if (!string.IsNullOrWhiteSpace(p.Nation)) origin.Add($"Nation: {p.Nation}");
        if (!string.IsNullOrWhiteSpace(p.Affiliation)) origin.Add($"Affiliation: {p.Affiliation}");
        if (origin.Count > 0) yield return string.Join(" | ", origin);

        if (!string.IsNullOrWhiteSpace(p.Description)) yield return Shorten(p.Description, MaxDescription);
    }

    private static string OrUnknown(string value) => string.IsNullOrWhiteSpace(value) ? "?" : value;

    private static void AddEquipment(DocumentBuilder doc, Item? item)
    {
        if (item == null)
        {
            doc.AddParagraph("none");
            return;
        }

        doc.AddParagraph(item.Name);
        doc.AddParagraph($"Bonuses: +{item.AttackBonus} ATK, +{item.ArmorBonus} ARM, +{item.HealthBonus} HP");
        AddDescription(doc, item.Description);
    }

    private static void AddAbility(DocumentBuilder doc, IAbility ability)
    {
        AddWrapped(doc, $"{ability.Name} - {ability.Describe()}");
        if (ability is CustomAbility custom)
        {
            AddDescription(doc, custom.Description);
        }
    }

    private static void AddDescription(DocumentBuilder doc, string description)
    {
        if (!string.IsNullOrWhiteSpace(description))
        {
            AddWrapped(doc, Shorten(description, MaxDescription));
        }
    }

    private static void AddWrapped(DocumentBuilder doc, string text)
    {
        foreach (string line in Wrap(text, WrapWidth))
        {
            doc.AddParagraph(line);
        }
    }
}
