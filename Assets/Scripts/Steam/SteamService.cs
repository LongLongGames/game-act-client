using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Steamworks;
using GameAct.UI;

namespace GameAct.Steam
{
    /// <summary>
    /// Steam Lobby = Steam 渠道的房间发现与组队。不写入 game-lobby。
    /// 支持列表估算延迟：Host 写 ping_loc，浏览端 EstimatePingTimeFromLocalHost。
    /// </summary>
    public class SteamService : ISteamService, IDisposable
    {
        public bool IsAvailable { get; private set; }
        public bool IsInitialized { get; private set; }
        public ulong SteamId { get; private set; }
        public string PersonaName { get; private set; } = "";
        public ulong CurrentLobbyId { get; private set; }
        public int LobbyMemberCount =>
            CurrentLobbyId == 0 ? 0 : SteamMatchmaking.GetNumLobbyMembers(new CSteamID(CurrentLobbyId));
        public string CurrentLobbyName { get; private set; } = "";

        public event Action<ulong> OnLobbyCreated;
        public event Action<ulong> OnLobbyEntered;
        public event Action OnLobbyLeft;
        public event Action OnLobbyMembersChanged;
        public event Action OnLobbyDataUpdated;
        public event Action<string> OnSteamError;

        Callback<LobbyCreated_t> _cbLobbyCreated;
        Callback<LobbyEnter_t> _cbLobbyEnter;
        Callback<GameLobbyJoinRequested_t> _cbJoinRequested;
        Callback<LobbyChatUpdate_t> _cbChatUpdate;
        Callback<LobbyDataUpdate_t> _cbLobbyDataUpdate;
        CallResult<LobbyMatchList_t> _crLobbyList;

        UniTaskCompletionSource<ulong> _createTcs;
        UniTaskCompletionSource<bool> _joinTcs;
        UniTaskCompletionSource<RoomListItem[]> _listTcs;

        HAuthTicket _lastTicket = HAuthTicket.Invalid;
        string _pendingRoomName;

        const string KeyPingLoc = "ping_loc";

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
                    ReportError("Steamworks DLL check failed");
                    return false;
                }

                if (!SteamAPI.Init())
                {
                    ReportError("SteamAPI.Init 失败：请先启动 Steam 客户端");
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
                _cbLobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdateCb);
                _crLobbyList = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);

                // 初始化中继网络，Ping Location 才有数据（通常要几秒）
                try { SteamNetworkingUtils.InitRelayNetworkAccess(); }
                catch (Exception e) { Debug.LogWarning("[Steam] InitRelayNetworkAccess: " + e.Message); }

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
            try
            {
                if (_lastTicket != HAuthTicket.Invalid)
                {
                    SteamUser.CancelAuthTicket(_lastTicket);
                    _lastTicket = HAuthTicket.Invalid;
                }
                SteamAPI.Shutdown();
            }
            catch { /* ignore */ }
            IsInitialized = false;
            IsAvailable = false;
            Debug.Log("[Steam] Shutdown");
        }

        public void Dispose() => Shutdown();

        public async UniTask<ulong> CreateLobbyAsync(string roomName, int maxMembers = 4)
        {
            if (!IsInitialized)
            {
                ReportError("Steam 未初始化");
                return 0;
            }

            _pendingRoomName = string.IsNullOrWhiteSpace(roomName) ? (PersonaName + " 的房间") : roomName.Trim();
            _createTcs = new UniTaskCompletionSource<ulong>();

            // Public：第二台电脑 RequestLobbyList 能刷到（测试用）
            var call = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxMembers);
            if (call == SteamAPICall_t.Invalid)
            {
                ReportError("CreateLobby 调用失败");
                return 0;
            }

            return await _createTcs.Task;
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
            CurrentLobbyName = "";
            OnLobbyLeft?.Invoke();
            Debug.Log("[Steam] LeaveLobby");
        }

        public async UniTask<RoomListItem[]> RequestLobbyListAsync()
        {
            if (!IsInitialized)
            {
                ReportError("Steam 未初始化");
                return Array.Empty<RoomListItem>();
            }

            _listTcs = new UniTaskCompletionSource<RoomListItem[]>();

            SteamMatchmaking.AddRequestLobbyListStringFilter("game", "act", ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(50);

            var call = SteamMatchmaking.RequestLobbyList();
            if (call == SteamAPICall_t.Invalid)
            {
                ReportError("RequestLobbyList 失败");
                return Array.Empty<RoomListItem>();
            }

            _crLobbyList.Set(call);
            return await _listTcs.Task;
        }

        public string[] GetLobbyMemberNames()
        {
            if (CurrentLobbyId == 0) return Array.Empty<string>();
            var id = new CSteamID(CurrentLobbyId);
            int n = SteamMatchmaking.GetNumLobbyMembers(id);
            var list = new List<string>(n);
            for (int i = 0; i < n; i++)
            {
                var mid = SteamMatchmaking.GetLobbyMemberByIndex(id, i);
                var name = SteamFriends.GetFriendPersonaName(mid);
                if (string.IsNullOrEmpty(name)) name = mid.m_SteamID.ToString();
                var owner = SteamMatchmaking.GetLobbyOwner(id);
                if (mid == owner) name += "（房主）";
                list.Add("· " + name);
            }
            return list.ToArray();
        }

        public void InviteFriendsOverlay()
        {
            if (!IsInitialized || CurrentLobbyId == 0)
            {
                ReportError("没有当前房间，无法邀请");
                return;
            }
            SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(CurrentLobbyId));
        }

        public bool InviteUserToLobby(ulong friendSteamId)
        {
            if (!IsInitialized || CurrentLobbyId == 0) return false;
            return SteamMatchmaking.InviteUserToLobby(new CSteamID(CurrentLobbyId), new CSteamID(friendSteamId));
        }

        public string GetAuthSessionTicketHex()
        {
            if (!IsInitialized) return null;
            try
            {
                if (_lastTicket != HAuthTicket.Invalid)
                {
                    SteamUser.CancelAuthTicket(_lastTicket);
                    _lastTicket = HAuthTicket.Invalid;
                }

                var buf = new byte[1024];
                uint size = 0;
                var identity = new SteamNetworkingIdentity();
                identity.SetSteamID(SteamUser.GetSteamID());
                var handle = SteamUser.GetAuthSessionTicket(buf, buf.Length, out size, ref identity);
                if (handle == HAuthTicket.Invalid || size == 0)
                {
                    ReportError("GetAuthSessionTicket 失败");
                    return null;
                }
                _lastTicket = handle;
                var hex = new StringBuilder((int)size * 2);
                for (int i = 0; i < size; i++)
                    hex.Append(buf[i].ToString("x2"));
                return hex.ToString();
            }
            catch (Exception e)
            {
                ReportError("Ticket 异常: " + e.Message);
                return null;
            }
        }

        void OnLobbyCreatedCb(LobbyCreated_t ev)
        {
            if (ev.m_eResult != EResult.k_EResultOK)
            {
                ReportError("房间创建失败: " + ev.m_eResult);
                _createTcs?.TrySetResult(0);
                return;
            }

            var id = ev.m_ulSteamIDLobby;
            CurrentLobbyId = id;
            var cid = new CSteamID(id);

            SteamMatchmaking.SetLobbyData(cid, "game", "act");
            SteamMatchmaking.SetLobbyData(cid, "ver", Application.version);
            SteamMatchmaking.SetLobbyData(cid, "name", _pendingRoomName ?? "房间");
            //WriteLocalPingLocation(cid);
            WriteLocalPingLocationWhenReadyAsync(cid).Forget();
            CurrentLobbyName = _pendingRoomName ?? "房间";

            Debug.Log($"[Steam] Lobby created {id} name={CurrentLobbyName}");
            OnLobbyCreated?.Invoke(id);
            _createTcs?.TrySetResult(id);
        }

        void OnLobbyEnterCb(LobbyEnter_t ev)
        {
            if (ev.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                ReportError("加入房间失败: " + ev.m_EChatRoomEnterResponse);
                _joinTcs?.TrySetResult(false);
                return;
            }

            CurrentLobbyId = ev.m_ulSteamIDLobby;
            var cid = new CSteamID(CurrentLobbyId);
            CurrentLobbyName = SteamMatchmaking.GetLobbyData(cid, "name");
            if (string.IsNullOrEmpty(CurrentLobbyName))
                CurrentLobbyName = "房间";

            // 房主进房后再写一次 ping_loc（创建时 location 可能还没就绪）
            if (SteamMatchmaking.GetLobbyOwner(cid) == SteamUser.GetSteamID())
                //WriteLocalPingLocation(cid);
                WriteLocalPingLocationWhenReadyAsync(cid).Forget();

            Debug.Log($"[Steam] Entered lobby {CurrentLobbyId} members={LobbyMemberCount}");
            OnLobbyEntered?.Invoke(CurrentLobbyId);
            _joinTcs?.TrySetResult(true);
            OnLobbyMembersChanged?.Invoke();
        }

        void OnJoinRequestedCb(GameLobbyJoinRequested_t ev)
        {
            Debug.Log($"[Steam] Friend invite → join {ev.m_steamIDLobby.m_SteamID}");
            JoinLobbyAsync(ev.m_steamIDLobby.m_SteamID).Forget();
        }

        void OnChatUpdateCb(LobbyChatUpdate_t ev)
        {
            if (ev.m_ulSteamIDLobby != CurrentLobbyId) return;
            OnLobbyMembersChanged?.Invoke();
        }

        void OnLobbyMatchList(LobbyMatchList_t ev, bool ioFailure)
        {
            if (ioFailure)
            {
                ReportError("Lobby 列表请求失败");
                _listTcs?.TrySetResult(Array.Empty<RoomListItem>());
                return;
            }

            var count = (int)ev.m_nLobbiesMatching;
            var items = new List<RoomListItem>(count);
            for (int i = 0; i < count; i++)
            {
                var lobby = SteamMatchmaking.GetLobbyByIndex(i);
                var name = SteamMatchmaking.GetLobbyData(lobby, "name");
                if (string.IsNullOrEmpty(name)) name = "Steam 房间";
                int members = SteamMatchmaking.GetNumLobbyMembers(lobby);
                int max = SteamMatchmaking.GetLobbyMemberLimit(lobby);
                if (max <= 0) max = 4;

                int ping = EstimatePingFromLobby(lobby);

                items.Add(new RoomListItem
                {
                    id = lobby.m_SteamID.ToString(),
                    title = name,
                    subtitle = "Steam P2P",
                    players = members,
                    maxPlayers = max,
                    pingMs = ping
                });
            }

            // 有 ping 的按延迟升序，未知排后
            items.Sort((a, b) =>
            {
                int pa = a.pingMs < 0 ? int.MaxValue : a.pingMs;
                int pb = b.pingMs < 0 ? int.MaxValue : b.pingMs;
                return pa.CompareTo(pb);
            });

            Debug.Log($"[Steam] Lobby list count={items.Count}");
            _listTcs?.TrySetResult(items.ToArray());
        }

        void OnLobbyDataUpdateCb(LobbyDataUpdate_t ev)
        {
            if (ev.m_ulSteamIDLobby != CurrentLobbyId) return;
            if (ev.m_bSuccess == 0) return;
            OnLobbyDataUpdated?.Invoke();
        }

        void ReportError(string msg)
        {
            Debug.LogError("[Steam] " + msg);
            OnSteamError?.Invoke(msg);
        }

        /// <summary>Host：把本地 Ping Location 写入 Lobby Data，供列表估算延迟。</summary>
        void WriteLocalPingLocation(CSteamID lobby)
        {
            try
            {
                var loc = default(SteamNetworkPingLocation_t);
                float age = SteamNetworkingUtils.GetLocalPingLocation(out loc);
                if (age < 0f)
                {
                    Debug.LogWarning("[Steam] PingLocation 尚未就绪，跳过写入（启动后等几秒再建房会有数据）");
                    return;
                }

                SteamNetworkingUtils.ConvertPingLocationToString(
                    ref loc, out string s, Constants.k_cchMaxSteamNetworkingPingLocationString);
                if (!string.IsNullOrEmpty(s))
                {
                    SteamMatchmaking.SetLobbyData(lobby, KeyPingLoc, s);
                    Debug.Log($"[Steam] Wrote ping_loc age={age:F1}s len={s.Length}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Steam] WriteLocalPingLocation: " + e.Message);
            }
        }
        /// <summary>Host：等 PingLocation 就绪再写入；失败会短时重试。</summary>
        async UniTask WriteLocalPingLocationWhenReadyAsync(CSteamID lobby, int maxRetries = 10, int intervalMs = 500)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // 主动催一下测量
                    SteamNetworkingUtils.CheckPingDataUpToDate(2f);

                    var loc = default(SteamNetworkPingLocation_t);
                    float age = SteamNetworkingUtils.GetLocalPingLocation(out loc);
                    if (age < 0f)
                    {
                        Debug.Log($"[Steam] PingLocation 未就绪 retry={i + 1}/{maxRetries}");
                        await UniTask.Delay(intervalMs);
                        continue;
                    }

                    SteamNetworkingUtils.ConvertPingLocationToString(
                        ref loc, out string s, Constants.k_cchMaxSteamNetworkingPingLocationString);
                    if (string.IsNullOrEmpty(s))
                    {
                        await UniTask.Delay(intervalMs);
                        continue;
                    }

                    SteamMatchmaking.SetLobbyData(lobby, KeyPingLoc, s);
                    Debug.Log($"[Steam] Wrote ping_loc age={age:F1}s len={s.Length} retry={i}");
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Steam] WriteLocalPingLocation: " + e.Message);
                    await UniTask.Delay(intervalMs);
                }
            }
            Debug.LogWarning("[Steam] WriteLocalPingLocation 最终失败（PingLocation 一直不可用）");
        }

        /// <summary>浏览端：读对方 ping_loc，用本地估算 RTT。失败返回 -1。</summary>
        static int EstimatePingFromLobby(CSteamID lobby)
        {
            try
            {
                var s = SteamMatchmaking.GetLobbyData(lobby, KeyPingLoc);
                Debug.Log($"[Steam] ping_loc raw len={(s == null ? 0 : s.Length)} lobby={lobby.m_SteamID}");
                if (string.IsNullOrEmpty(s)) return -1;

                var remote = default(SteamNetworkPingLocation_t);
                if (!SteamNetworkingUtils.ParsePingLocationString(s, out remote))
                {
                    Debug.LogWarning("[Steam] ParsePingLocationString 失败");
                    return -1;
                }

                int ms = SteamNetworkingUtils.EstimatePingTimeFromLocalHost(ref remote);
                Debug.Log($"[Steam] EstimatePing={ms}");
                return ms >= 0 ? ms : -1;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Steam] EstimatePingFromLobby: " + e.Message);
                return -1;
            }
        }
    }
}
