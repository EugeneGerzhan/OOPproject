using System;
using System.Collections.Generic;
using System.Linq;

namespace OOPproject.SOLID;

/// <summary>
/// Object representation of the parsed arguments of a terminal command (the "Args" of the design).
/// Positional words are the words that are not options, in the order they were typed.
/// Options are written as <c>--name value</c>, or as a flag <c>--name</c> (the value is empty).
/// Names of options are case-insensitive. The arguments are copied, so later changes of the source do not affect them.
/// </summary>
public sealed class Args
{
    public Args(IEnumerable<string>? positional = null, IDictionary<string, string>? options = null)
    {
        Positional = (positional ?? Array.Empty<string>()).ToArray();
        Options = options == null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(options, StringComparer.OrdinalIgnoreCase);
    }

    public string[] Positional { get; }

    public Dictionary<string, string> Options { get; }

    public bool Has(string option) => Options.ContainsKey(option);

    /// <summary>The value of an option; null when the option is absent.</summary>
    public string? Get(string option) => Options.TryGetValue(option, out var value) ? value : null;

    /// <summary>The positional words starting from <paramref name="skip"/> joined with spaces.</summary>
    public string Joined(int skip = 0) => string.Join(" ", Positional.Skip(skip));
}