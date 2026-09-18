using System.Threading;
using Cysharp.Threading.Tasks;
using GameAct.Network;

namespace GameAct.Services
{
    public interface IPlayerService
    {
        PlayerProfile Profile { get; }

        UniTask<(bool ok, PlayerProfile profile, string error)> FetchProfileAsync(CancellationToken ct = default);
    }
}
