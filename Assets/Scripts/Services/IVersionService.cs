using System.Threading;
using Cysharp.Threading.Tasks;
using GameAct.Network;

namespace GameAct.Services
{
    public interface IVersionService
    {
        /// <summary>
        /// 调用游戏服公开接口 /api/v1/game/version-check。
        /// 返回 (ok, response 或 null, error)。
        /// force_update 时调用方应阻止进入游戏。
        /// </summary>
        UniTask<(bool ok, VersionCheckResponse resp, string error)> CheckVersionAsync(CancellationToken ct = default);
    }
}
