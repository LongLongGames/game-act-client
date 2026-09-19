using System;
using Cysharp.Threading.Tasks;
using GameAct.UI;

namespace GameAct.Steam
{
    /// <summary>
    /// Steam 渠道：Lobby 发现 / 创建 / 加入 / 邀请。
    /// 与官服 game-lobby 无关。
    /// </summary>
    public interface ISteamService
    {
        bool IsAvailable { get; }
        bool IsInitialized { get; }
        ulong SteamId { get; }
        string PersonaName { get; }

        bool Init();
        void Shutdown();
        void RunCallbacks();

        /// <summary>创建公开可搜的 Lobby（P2P 房）。</summary>
        UniTask<ulong> CreateLobbyAsync(string roomName, int maxMembers = 4);

        UniTask<bool> JoinLobbyAsync(ulong lobbyId);
        void LeaveLobby();

        ulong CurrentLobbyId { get; }
        int LobbyMemberCount { get; }
        string CurrentLobbyName { get; }

        /// <summary>RequestLobbyList，返回可展示的房间项。</summary>
        UniTask<RoomListItem[]> RequestLobbyListAsync();

        string[] GetLobbyMemberNames();

        void InviteFriendsOverlay();
        bool InviteUserToLobby(ulong friendSteamId);

        string GetAuthSessionTicketHex();

        event Action<ulong> OnLobbyCreated;
        event Action<ulong> OnLobbyEntered;
        event Action OnLobbyLeft;
        event Action OnLobbyMembersChanged;
        event Action<string> OnSteamError;
    }
}
