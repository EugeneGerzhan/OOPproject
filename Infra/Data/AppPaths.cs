using System;
using System.IO;

namespace Infra.Data;

/// <summary>
/// Where the application keeps its files: a folder of its own in the data folder of the user
/// (on Windows <c>%LOCALAPPDATA%\CharacterBattler</c>, on Linux <c>~/.local/share/CharacterBattler</c>).
/// The location can be changed with the environment variable <c>CHARACTER_BATTLER_DATA</c>.
/// </summary>
public sealed class AppPaths
{
    public const string AppFolderName = "CharacterBattler";
    public const string DataDirectoryVariable = "CHARACTER_BATTLER_DATA";

    public AppPaths(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            throw new ArgumentException("The data directory cannot be empty.", nameof(dataDirectory));
        }

        DataDirectory = Path.GetFullPath(dataDirectory);
    }

    public string DataDirectory { get; }

    /// <summary>The file with the saves, the results and the documents.</summary>
    public string SavesFile => Path.Combine(DataDirectory, "saves.json");

    /// <summary>The folder of the cache of pictures.</summary>
    public string ImagesDirectory => Path.Combine(DataDirectory, "images");

    /// <summary>The paths for the current user.</summary>
    public static AppPaths ForCurrentUser()
    {
        string? custom = Environment.GetEnvironmentVariable(DataDirectoryVariable);
        if (!string.IsNullOrWhiteSpace(custom))
        {
            return new AppPaths(custom);
        }

        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(root))
        {
            root = AppContext.BaseDirectory;
        }
        return new AppPaths(Path.Combine(root, AppFolderName));
    }

    /// <summary>
    /// Older versions kept <c>saves.json</c> next to the program. Copies such a file to the data directory,
    /// once: nothing is copied when the new file already exists.
    /// </summary>
    /// <returns>True when a file was copied.</returns>
    public bool MigrateLegacySaves(string legacyFile)
    {
        if (File.Exists(SavesFile) || !File.Exists(legacyFile))
        {
            return false;
        }

        Directory.CreateDirectory(DataDirectory);
        File.Copy(legacyFile, SavesFile);
        return true;
    }
}