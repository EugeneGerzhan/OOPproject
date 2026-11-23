using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Infra.WebApi;

namespace ProjectTests.Mocks
{
    /// <summary>Stub of the HTTP layer: answers from a function and records the requested URLs.</summary>
    public class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        public List<string> RequestedUrls { get; } = new List<string>();

        public static StubHttpMessageHandler Json(string json) =>
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        public static StubHttpMessageHandler Status(HttpStatusCode status) =>
            new StubHttpMessageHandler(_ => new HttpResponseMessage(status));

        public static StubHttpMessageHandler Throwing(Exception exception) =>
            new StubHttpMessageHandler(_ => throw exception);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUrls.Add(request.RequestUri!.ToString());
            return Task.FromResult(_respond(request));
        }
    }

    /// <summary>Fake remote database kept in memory.</summary>
    public class FakeGameDataApi : IGameDataApi
    {
        public Dictionary<string, RemoteCharacter> Characters { get; } =
            new Dictionary<string, RemoteCharacter>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, RemoteWeapon> Weapons { get; } =
            new Dictionary<string, RemoteWeapon>(StringComparer.OrdinalIgnoreCase);

        public List<string> Calls { get; } = new List<string>();

        public Task<IReadOnlyList<string>> GetCharacterIdsAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("characters");
            return Task.FromResult<IReadOnlyList<string>>(Characters.Keys.ToList());
        }

        public Task<RemoteCharacter> GetCharacterAsync(string id, CancellationToken cancellationToken = default)
        {
            Calls.Add($"characters/{id}");
            if (!Characters.TryGetValue(id, out var character))
            {
                throw new RemoteApiException($"Character '{id}' was not found in the remote database.");
            }
            return Task.FromResult(character);
        }

        public Task<IReadOnlyList<string>> GetWeaponIdsAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("weapons");
            return Task.FromResult<IReadOnlyList<string>>(Weapons.Keys.ToList());
        }

        public Task<RemoteWeapon> GetWeaponAsync(string id, CancellationToken cancellationToken = default)
        {
            Calls.Add($"weapons/{id}");
            if (!Weapons.TryGetValue(id, out var weapon))
            {
                throw new RemoteApiException($"Weapon '{id}' was not found in the remote database.");
            }
            return Task.FromResult(weapon);
        }
    }

    /// <summary>Ready-made remote objects.</summary>
    public static class RemoteSamples
    {
        public static RemoteCharacter Albedo() => new RemoteCharacter(
            "albedo", "Albedo", "Kreideprinz", "Geo", "Sword", "Mondstadt", "Knights of Favonius", 5,
            "Chief Alchemist of the Knights of Favonius.",
            new List<RemoteTalent>
            {
                new RemoteTalent("Favonius Bladework - Weiss", "Normal Attack", "Performs rapid strikes."),
                new RemoteTalent("Abiogenesis: Solar Isotoma", "Elemental Skill", "Creates a Solar Isotoma."),
                new RemoteTalent("Rite of Progeniture: Tectonic Tide", "Elemental Burst", "Unleashes blades.")
            },
            new List<RemoteTalent> { new RemoteTalent("Calcite Might", "Unlocked at Ascension 1", "Strengthens Solar Isotoma.") });

        public static RemoteCharacter Simple(string id, string name, int rarity = 4) => new RemoteCharacter(
            id, name, "", "Pyro", "Bow", "Mondstadt", "", rarity, "",
            new List<RemoteTalent> { new RemoteTalent("Strike", "Elemental Burst", "") },
            new List<RemoteTalent>());

        public static RemoteWeapon SkywardBlade() => new RemoteWeapon(
            "skyward-blade", "Skyward Blade", "Sword", 5, 46, "Energy Recharge",
            "Sky-Piercing Fang", "Raises CRIT Rate.\nEmpowers attacks after a burst.", "Gacha");

        public static RemoteWeapon DullBlade() => new RemoteWeapon(
            "dull-blade", "Dull Blade", "Sword", 1, 23, "", "", "", "Drop");
    }

    /// <summary>xUnit class fixture: raw JSON samples shared by parser and client tests.</summary>
    public class RemoteSamplesFixture
    {
        public string AlbedoJson { get; } = Q(
            "{ 'name': 'Albedo', 'title': 'Kreideprinz', 'vision': 'Geo', 'weapon': 'Sword', " +
            "'nation': 'Mondstadt', 'affiliation': 'Knights of Favonius', 'rarity': 5, " +
            "'description': 'Chief Alchemist of the Knights of Favonius.', " +
            "'skillTalents': [ " +
            "{ 'name': 'Favonius Bladework - Weiss', 'unlock': 'Normal Attack', 'description': 'Performs rapid strikes.' }, " +
            "{ 'name': 'Abiogenesis: Solar Isotoma', 'unlock': 'Elemental Skill', 'description': 'Creates a Solar Isotoma.' }, " +
            "{ 'name': 'Rite of Progeniture: Tectonic Tide', 'unlock': 'Elemental Burst', 'description': 'Unleashes blades.' } ], " +
            "'passiveTalents': [ { 'name': 'Calcite Might', 'unlock': 'Unlocked at Ascension 1', 'description': 'Strengthens Solar Isotoma.' } ] }");

        public string WeaponJson { get; } = Q(
            "{ 'name': 'Skyward Blade', 'type': 'Sword', 'rarity': 5, 'baseAttack': 46, " +
            "'subStat': 'Energy Recharge', 'passiveName': 'Sky-Piercing Fang', " +
            "'passiveDesc': 'Raises CRIT Rate.', 'location': 'Gacha' }");

        public string IdsJson { get; } = "[\"albedo\",\"amber\",\"bennett\"]";

        private static string Q(string text) => text.Replace('\'', '"');
    }
}