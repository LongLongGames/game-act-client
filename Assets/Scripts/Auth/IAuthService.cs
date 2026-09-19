using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameAct.Auth
{
    public interface IAuthService
    {
        bool IsLoggedIn { get; }

        /// <summary>开发：官方用户名密码。</summary>
        UniTask<(bool ok, string error)> LoginAsync(string username, string password, CancellationToken ct = default);

        /// <summary>
        /// 正式：Steam 登录/静默注册。
        /// MP provider=steam，payload 带 steam_id + session_ticket。
        /// </summary>
        UniTask<(bool ok, string error)> LoginWithSteamAsync(ulong steamId, string sessionTicketHex, CancellationToken ct = default);

        void TryRestoreToken();
        UniTask<bool> ValidateSessionAsync(CancellationToken ct = default);
        void Logout();
    }
}
