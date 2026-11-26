using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Infra.Data
{
    public abstract class SaveBase
    {
        public string SaveName { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
    }

    public class CharacterSave : SaveBase
    {
        public List<CharacterData> Characters { get; set; } = new List<CharacterData>();
        public List<AbilityData> Abilities { get; set; } = new List<AbilityData>();
        public List<ItemData> Items { get; set; } = new List<ItemData>();
        public List<CharacterItems> ItemLinks { get; set; } = new List<CharacterItems>();
        public List<CharacterAbilities> AbilityLinks { get; set; } = new List<CharacterAbilities>();
        /// <summary>The battle that was in progress when the game was saved (null when there was none).</summary>
        public BattleStateData? Battle { get; set; }
    }

    public class TextSave : SaveBase
    {
        public TextNodeData? TextRoot { get; set; }
    }
    
    /// <summary>The version of the format of the saves file.</summary>
    public static class RepositoryFormat
    {
        /// <summary>Raise it when the format changes in a way older programs cannot read.</summary>
        public const int CurrentVersion = 1;
    }
    /// <summary>Everything that is written to the file: all named saves of both systems.</summary>
    public class Repository
    {
        public DateTime LastModified { get; set; }
        public Dictionary<string, CharacterSave> CharacterSaves { get; set; } = new Dictionary<string, CharacterSave>();
        public Dictionary<string, TextSave> TextSaves { get; set; } = new Dictionary<string, TextSave>();
        public List<BattleResultData> BattleResults { get; set; } = new List<BattleResultData>();

        /// <summary>The version of the format this file was written in.</summary>
        public int Version { get; set; } = RepositoryFormat.CurrentVersion;

        /// <summary>The name of the character save (an autosave counts too) that was written last.</summary>
        public string? LastCharacterSave { get; set; }
    }
}
