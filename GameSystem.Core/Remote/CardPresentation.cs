using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GameSystem.Core.Remote
{
    /// <summary>
    /// How a character card looks as text: the stars, the line of facts, a short description, the letter of the
    /// placeholder and the accent colour of the vision. It has no graphics in it, so it can be tested without a window;
    /// the window only turns it into controls.
    /// </summary>
    public sealed record CardPresentation(string Name, string Title, string Stars, string Info,
                                          string ShortDescription, string Initial, string AccentColor)
    {
        public const int ShortDescriptionLength = 140;
        public const string DefaultColor = "#9AA0A6";

        private static readonly Dictionary<string, string> VisionColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Anemo"] = "#4FC3A1",
            ["Geo"] = "#D9A441",
            ["Electro"] = "#A96BE0",
            ["Dendro"] = "#7CB342",
            ["Hydro"] = "#3F8EF0",
            ["Pyro"] = "#E8603C",
            ["Cryo"] = "#6FCBE0"
        };

        public static CardPresentation From(CharacterCard card)
        {
            ArgumentNullException.ThrowIfNull(card);

            string name = card.Name.Trim();
            string facts = string.Join(" \u00B7 ",
                new[] { card.Vision, card.WeaponType, card.Nation }.Where(s => !string.IsNullOrWhiteSpace(s)));

            return new CardPresentation(
                name,
                card.Title,
                new string('\u2605', Math.Clamp(card.Rarity, 0, 5)),
                facts,
                Shorten(card.Description, ShortDescriptionLength),
                name.Length > 0 ? name.Substring(0, 1).ToUpperInvariant() : "?",
                VisionColors.TryGetValue(card.Vision ?? string.Empty, out var color) ? color : DefaultColor);
        }

        /// <summary>One line of at most <paramref name="maxLength"/> characters (a cut text ends with an ellipsis).</summary>
        public static string Shorten(string? text, int maxLength)
        {
            string flat = Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();
            return flat.Length <= maxLength ? flat : flat.Substring(0, maxLength).TrimEnd() + "\u2026";
        }
    }
}