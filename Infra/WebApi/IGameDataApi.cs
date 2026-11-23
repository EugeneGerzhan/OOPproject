using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Infra.WebApi
{
    /// <summary>Source of characters and weapons (real HTTP service, offline sample, test fake...).</summary>
    public interface IGameDataApi
    {
        Task<IReadOnlyList<string>> GetCharacterIdsAsync(CancellationToken cancellationToken = default);
        Task<RemoteCharacter> GetCharacterAsync(string id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<string>> GetWeaponIdsAsync(CancellationToken cancellationToken = default);
        Task<RemoteWeapon> GetWeaponAsync(string id, CancellationToken cancellationToken = default);
    }
}
