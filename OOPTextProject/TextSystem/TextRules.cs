using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OOPTextProject.TextSystem;

/// <summary>
/// Validation rules shared by every way of creating document elements
/// (<see cref="TextFactory"/>, the document builder and copying).
/// </summary>
public static class TextRules
{
    /// <summary>Validates and normalizes (trims) the title of a heading.</summary>
    /// <exception cref="ArgumentException">The title is empty, contains '/' or is '.' / '..'.</exception>
    public static string ValidateHeadingName(string? name)
    {
        string value = (name ?? string.Empty).Trim();

        if (value.Length == 0)
        {
            throw new ArgumentException("Heading title cannot be empty.");
        }
        if (value.Contains('/'))
        {
            throw new ArgumentException("Heading title cannot contain '/'.");
        }
        if (value == "." || value == "..")
        {
            throw new ArgumentException("Heading title cannot be '.' or '..'.");
        }

        return value;
    }

    /// <summary>Validates the content of a paragraph.</summary>
    /// <exception cref="ArgumentException">The content is empty or whitespace.</exception>
    public static string ValidateParagraph(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Paragraph content cannot be empty.");
        }

        return content;
    }
}