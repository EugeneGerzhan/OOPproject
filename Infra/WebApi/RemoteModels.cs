using System;
using System.Collections.Generic;

namespace Infra.WebApi
{
    /// <summary>A talent of a remote character: name, kind ("Elemental Burst"...) and description.</summary>
    public record RemoteTalent(string Name, string Unlock, string Description);

    /// <summary>Character exactly as the remote database describes it (no game stats there).</summary>
    public record RemoteCharacter(
     string Id, string Name, string Title, string Vision, string WeaponType,
     string Nation, string Affiliation, int Rarity, string Description,
     IReadOnlyList<RemoteTalent> SkillTalents, IReadOnlyList<RemoteTalent> PassiveTalents,
     string ImageUrl = "");

    /// <summary>Weapon exactly as the remote database describes it.</summary>
    public record RemoteWeapon(
        string Id, string Name, string Type, int Rarity, int BaseAttack,
        string SubStat, string PassiveName, string PassiveDescription, string Location);

    /// <summary>Any failure while talking to the remote service (network, status code, bad data).</summary>
    public class RemoteApiException : Exception
    {
        public RemoteApiException(string message) : base(message) { }

        public RemoteApiException(string message, Exception inner) : base(message, inner) { }
    }
}
