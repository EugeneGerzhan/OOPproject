using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace OOPTextProject.TextSystem.Formatting;

/// <summary>
/// The list of available format factories. Factories are stateless, so sharing one instance is safe.
/// To add a new format, write a new <see cref="ITextFormatFactory"/> and add it to <c>All</c>.
/// </summary>
public static class TextFormats
{
    /// <summary>The default format (plain text).</summary>
    public static ITextFormatFactory Plain { get; } = new PlainTextFormatFactory();

    private static readonly ITextFormatFactory[] All = [Plain, new HtmlFormatFactory(), new MarkdownFormatFactory()];

    /// <summary>Names of all available formats.</summary>
    public static IReadOnlyList<string> Names => All.Select(factory => factory.Name).ToList();

    /// <summary>Finds a format by its name (case-insensitive).</summary>
    public static bool TryGet(string name, [NotNullWhen(true)] out ITextFormatFactory? factory)
    {
        factory = All.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        return factory != null;
    }
}
