using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OOPTextProject.TextSystem.Formatting;

namespace OOPproject.SOLID;

/// <summary>Reads the shared <c>--format</c> option of the commands that print documents.</summary>
public static class FormatOption
{
    /// <summary>
    /// Resolves the requested format; plain text when the option is absent.
    /// Reports an unknown format to the user and returns false.
    /// </summary>
    public static bool TryResolve(Dictionary<string, string> opts, IPresenter ui, out ITextFormatFactory formats)
    {
        formats = TextFormats.Plain;

        if (!opts.TryGetValue("--format", out var name) || string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        if (TextFormats.TryGet(name.Trim(), out var found))
        {
            formats = found;
            return true;
        }

        ui.Show($"Unknown format '{name}'. Available: {string.Join(", ", TextFormats.Names)}.");
        return false;
    }
}
