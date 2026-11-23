using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OOPproject.SOLID;

/// <summary>A line of text split into the name of the command and its arguments.</summary>
public sealed record ParsedCommand(string Selector, Args Args);

/// <summary>Turns the raw text typed by the user into a <see cref="ParsedCommand"/> (the "ArgParser" of the design).</summary>
public interface IArgsParser
{
    /// <returns>The parsed command; null for an empty line.</returns>
    ParsedCommand? ParseArgs(string rawInput);
}

/// <summary>
/// The default parser. The first word is the selector of the command, the rest are positional arguments and options
/// (see <see cref="CliArguments"/>). Text in quotes is one word.
/// </summary>
public sealed class ArgsParser : IArgsParser
{
    public ParsedCommand? ParseArgs(string rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return null;
        }

        var tokens = Tokenize(rawInput);
        if (tokens.Count == 0)
        {
            return null;
        }

        var arguments = new CliArguments(tokens.Skip(1).ToArray());
        return new ParsedCommand(tokens[0], new Args(arguments.Args, arguments.Options));
    }

    /// <summary>
    /// Splits input into tokens. A quote opens a quoted token only at the START of a token,
    /// so apostrophes inside words (п'ять, don't) stay normal characters.
    /// Supported quotes: "..."  '...'  “...”  ‘...’  «...»
    /// </summary>
    public static IReadOnlyList<string> Tokenize(string input)
    {
        var results = new List<string>();
        var token = new StringBuilder();
        bool hasToken = false;
        char closingQuote = '\0';

        foreach (char c in input)
        {
            if (closingQuote != '\0')
            {
                if (c == closingQuote)
                {
                    closingQuote = '\0';
                }
                else
                {
                    token.Append(c);
                }
                continue;
            }

            char closing = hasToken ? '\0' : GetClosingQuote(c);

            if (closing != '\0')
            {
                closingQuote = closing;
                hasToken = true;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (hasToken)
                {
                    results.Add(token.ToString());
                    token.Clear();
                    hasToken = false;
                }
            }
            else
            {
                token.Append(c);
                hasToken = true;
            }
        }

        if (hasToken)
        {
            results.Add(token.ToString());
        }

        return results;
    }

    private static char GetClosingQuote(char opening)
    {
        switch (opening)
        {
            case '"': return '"';
            case '\'': return '\'';
            case '\u201C': return '\u201D'; // “ ”
            case '\u2018': return '\u2019'; // ‘ ’
            case '\u00AB': return '\u00BB'; // « »
            default: return '\0';
        }
    }
}