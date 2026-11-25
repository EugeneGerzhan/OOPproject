using System;
using System.Collections.Generic;

namespace Infra.Data
{
    /// <summary>Kinds of text nodes stored in a save.</summary>
    public static class TextNodeKinds
    {
        public const string Root = "root";
        public const string Heading = "heading";
        public const string Paragraph = "paragraph";
    }

    // ---------- Text system ----------

    /// <summary>One node of the document tree (root, heading or paragraph).</summary>
    public record TextNodeData(int Id, string Kind, string Text, List<TextNodeData>? Children = null);

    // ---------- Characters system (relational layout, many-to-many through link records) ----------

    /// <summary>Profile of a character imported from the remote database (null for built-in classes).</summary>
    public record ProfileData(string RemoteId, string Title, string Vision, string WeaponType,
                              string Nation, string Affiliation, string Description, int Rarity);

    public record CharacterData(int Id, string Name, string Class, int Health, int MaxHealth, int Armor,
                            int AttackPower, int UniqueAbilityId, ProfileData? Profile = null,
                            double Resist = 0.0, int Speed = 100);

    public record AbilityData(int Id, string Type, string Name, int Hits, int AttackBonus,
                              string? Description = null, string? Kind = null, double ResistPenetration = 0.0);

    /// <summary>One step (action) of a battle, as saved.</summary>
    public record TurnData(int Number, int Round, string Actor, string Action, string? Target, List<string> Messages);

    /// <summary>A battle in progress, as saved: who fights, whose turn it is and what has happened so far.</summary>
    public record BattleStateData(List<int> ParticipantIds, int Round, List<int> Queue, int QueueIndex,
                                  int Actions, List<TurnData> History);

    public record ItemData(int Id, string Name, int HealthBonus, int ArmorBonus, int AttackBonus,
                           string? Description = null);

    public record CharacterItems(int Id, int CharacterId, int ItemId);

    public record CharacterAbilities(int Id, int CharacterId, int AbilityId);
    /// <summary>The result of a finished battle of the text game.</summary>
    public record BattleResultData(string? Winner, List<string> Participants, int Rounds, int Actions, DateTime FinishedAt);
}