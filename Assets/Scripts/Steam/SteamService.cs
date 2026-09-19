using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Steamworks;

namespace GameAct.Steam
{
    /// <summary>
    /// Steamworks.NET 封装。AppId 默认 480（Spacewar 测试），正式包改 steam_appid.txt。
    /// 必须在主线程 Init / RunCallbacks。
    /// </summary>
    public class SteamService : ISteamService, IDisposable
    {
        public bool IsAvailable { get; private set; }
        public bool IsInitialized { get; private set; }
        public ulong SteamId { get; private set; }
        public string PersonaName { get; private set; } = "";
        public ulong CurrentLobbyId { get; private set; }
        public int LobbyMemberCount => CurrentLobbyId == 0 ? 0 : SteamMatchmaking.GetNumLobbyMembers(new CSteamID(CurrentLobbyId));

        public event Action<ulong> OnLobbyCreated;
        public event Action<ulong> OnLobbyEntered;
        public event Action OnLobbyLeft;
        public event Action<string> OnSteamError;

        Callback<LobbyCreated_t> _cbLobbyCreated;
        Callback<LobbyEnter_t> _cbLobbyEnter;
        Callback<GameLobbyJoinRequested_t> _cbJoinRequested;
        Callback<LobbyChatUpdate_t> _cbChatUpdate;

        UniTaskCompletionSource<ulong> _createTcs;
        UniTaskCompletionSource<bool> _joinTcs;

        public bool Init()
        {
            if (IsInitialized) return true;

            try
            {
                if (!Packsize.Test())
                {
                    ReportError("Steamworks Packsize mismatch");
                    return false;
                }
                if (!DllCheck.Test())
                {
                    ReportError("Steamworks DLL check failed（确认 steam_api64.dll 在位）");
                    return false;
                }

                // steam_appid.txt 已在工程根；正式发布由 Steam 注入
                if (!SteamAPI.Init())
                {
                    ReportError("SteamAPI.Init 失败：请先启动 Steam 客户端，或检查 steam_appid.txt");
                    IsAvailable = false;
                    return false;
                }

                IsAvailable = true;
                IsInitialized = true;
                SteamId = SteamUser.GetSteamID().m_SteamID;
                PersonaName = SteamFriends.GetPersonaName() ?? "";

                _cbLobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreatedCb);
                _cbLobbyEnter = Callback<LobbyEnter_t>.Create(OnLobbyEnterCb);
                _cbJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequestedCb);
                _cbChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnChatUpdateCb);

                Debug.Log($"[Steam] Init OK  id={SteamId} name={PersonaName}");
                return true;
            }
            catch (Exception e)
            {
                ReportError("Steam Init 异常: " + e.Message);
                IsAvailable = false;
                IsInitialized = false;
                return false;
            }
        }

        public void RunCallbacks()
        {
            if (!IsInitialized) return;
            try { SteamAPI.RunCallbacks(); }
            catch (Exception e) { Debug.LogWarning("[Steam] RunCallbacks: " + e.Message); }
        }

        public void Shutdown()
        {
            if (!IsInitialized) return;
            LeaveLobby();
            try { SteamAPI.Shutdown(); } catch { /* ignore */ }
            IsInitialized = false;
            IsAvailable = false;
            Debug.Log("[Steam] Shutdown");
        }

        public void Dispose() => Shutdown();

        public async UniTask<ulong> CreateLobbyAsync(int maxMembers = 4)
        {
            if (!IsInitialized)
            {
                ReportError("Steam 未初始化");
                return 0;
            }

            _createTcs = new UniTaskCompletionSource<ulong>();
            var result = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxMembers);
            if (result == SteamAPICall_t.Invalid)
            {
                ReportError("CreateLobby 调用失败");
                return 0;
            }

            var lobbyId = await _createTcs.Task;
            return lobbyId;
        }

        public async UniTask<bool> JoinLobbyAsync(ulong lobbyId)
        {
            if (!IsInitialized || lobbyId == 0) return false;

            _joinTcs = new UniTaskCompletionSource<bool>();
            SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
            return await _joinTcs.Task;
        }

        public void LeaveLobby()
        {
            if (CurrentLobbyId == 0) return;
            SteamMatchmaking.LeaveLobby(new CSteamID(CurrentLobbyId));
            CurrentLobbyId = 0;
            OnLobbyLeft?.Invoke();
            Debug.Log("[Steam] LeaveLobby");
        }

        public void InviteFriendsOverlay()
        {
            if (!IsInitialized || CurrentLobbyId == 0)
            {
                ReportError("没有当前 Lobby，无法邀请");
                return;
            }
            SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(CurrentLobbyId));
        }

        public bool InviteUserToLobby(ulong friendSteamId)
        {
            if (!IsInitialized || CurrentLobbyId == 0) return false;
            return SteamMatchmaking.InviteUserToLobby(new CSteamID(CurrentLobbyId), new CSteamID(friendSteamId));
        }

        void OnLobbyCreatedCb(LobbyCreated_t ev)
        {
            if (ev.m_eResult != EResult.k_EResultOK)
            {
                ReportError("Lobby 创建失败: " + ev.m_eResult);
                _createTcs?.TrySetResult(0);
                return;
            }

            var id = ev.m_ulSteamIDLobby;
            CurrentLobbyId = id;
            // 可加入元数据，便于 ServerList / 好友发现
            SteamMatchmaking.SetLobbyData(new CSteamID(id), "game", "act");
            SteamMatchmaking.SetLobbyData(new CSteamID(id), "ver", Application.version);

            Debug.Log($"[Steam] Lobby created {id}");
            OnLobbyCreated?.Invoke(id);
            _createTcs?.TrySetResult(id);
        }

        void OnLobbyEnterCb(LobbyEnter_t ev)
        {
            if (ev.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                ReportError("加入 Lobby 失败: " + ev.m_EChatRoomEnterResponse);
                _joinTcs?.TrySetResult(false);
                return;
            }

            CurrentLobbyId = ev.m_ulSteamIDLobby;
            Debug.Log($"[Steam] Entered lobby {CurrentLobbyId} members={LobbyMemberCount}");
            OnLobbyEntered?.Invoke(CurrentLobbyId);
            _joinTcs?.TrySetResult(true);
        }

        void OnJoinRequestedCb(GameLobbyJoinRequested_t ev)
        {
            Debug.Log($"[Steam] Friend invite → join lobby {ev.m_steamIDLobby.m_SteamID}");
            JoinLobbyAsync(ev.m_steamIDLobby.m_SteamID).Forget();
        }

        void OnChatUpdateCb(LobbyChatUpdate_t ev)
        {
            Debug.Log($"[Steam] Lobby member change lobby={ev.m_ulSteamIDLobby} members={LobbyMemberCount}");
        }

        void ReportError(string msg)
        {
            Debug.LogWarning("[Steam] " + msg);
            OnSteamError?.Invoke(msg);
        }
    }
}
