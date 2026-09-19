using System;
using Cysharp.Threading.Tasks;

namespace GameAct.Steam
{
    /// <summary>
    /// Steamworks 抽象。P1：Init / Lobby / 邀请。P2P 传输后续接到 LiteNetLib。
    /// </summary>
    public interface ISteamService
    {
        bool IsAvailable { get; }
        bool IsInitialized { get; }
        ulong SteamId { get; }
        string PersonaName { get; }

        /// <summary>初始化 Steam（需 Steam 客户端运行，AppId 读 steam_appid.txt）。</summary>
        bool Init();

        void Shutdown();

        /// <summary>每帧调用，处理 Steam 回调。</summary>
        void RunCallbacks();

        /// <summary>创建可加入的 Lobby，返回 LobbyId（0 失败）。</summary>
        UniTask<ulong> CreateLobbyAsync(int maxMembers = 4);

        /// <summary>加入已有 Lobby。</summary>
        UniTask<bool> JoinLobbyAsync(ulong lobbyId);

        void LeaveLobby();

        ulong CurrentLobbyId { get; }
        int LobbyMemberCount { get; }

        /// <summary>邀请好友到当前 Lobby（弹出 Overlay）。</summary>
        void InviteFriendsOverlay();

        /// <summary>向指定好友发 Lobby 邀请。</summary>
        bool InviteUserToLobby(ulong friendSteamId);

        event Action<ulong> OnLobbyCreated;
        event Action<ulong> OnLobbyEntered;
        event Action OnLobbyLeft;
        event Action<string> OnSteamError;
    }
}
