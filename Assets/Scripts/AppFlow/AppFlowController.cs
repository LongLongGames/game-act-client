using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using GameAct.Auth;
using GameAct.Network;
using GameAct.Services;
using GameAct.UI;
using GameAct.Steam;
using GameAct.Net;

namespace GameAct.AppFlow
{
    /// <summary>
    /// Steam 渠道多人：只走 Steam Lobby（创建/列表/加入/邀请）。
    /// 与官服 game-lobby / Dedicated ServerList 无关。
    /// </summary>
    public class AppFlowController : IAppFlow
    {
        public AppState State { get; private set; } = AppState.Boot;

        readonly IVersionService _version;
        readonly IAuthService _auth;
        readonly IPlayerService _player;
        readonly IHttpClient _http;
        readonly ILoginView _loginView;
        readonly IHomeView _homeView;
        readonly ISteamService _steam;
        readonly INetSession _net;
        readonly ApiConfig _config;

        bool _unauthorizedHandling;
        bool _isRoomHost;

        public AppFlowController(
            IVersionService version,
            IAuthService auth,
            IPlayerService player,
            IHttpClient http,
            ILoginView loginView,
            IHomeView homeView,
            ISteamService steam = null,
            INetSession net = null,
            ApiConfig config = null,
            object unusedLobby = null) // 保留参数兼容旧 Bootstrap，忽略
        {
            _version = version;
            _auth = auth;
            _player = player;
            _http = http;
            _loginView = loginView;
            _homeView = homeView;
            _steam = steam;
            _net = net;
            _config = config ?? new ApiConfig();
        }

        public async UniTask StartAsync(CancellationToken ct = default)
        {
            _http.Unauthorized += OnUnauthorized;
            _loginView.OnDevLoginSubmitted += HandleDevLoginSubmitted;
            _loginView.OnSteamEnterClicked += HandleSteamEnterClicked;

            _homeView.OnSinglePlayerClicked += () => _homeView.SetStatus("单人模式：占位");
            _homeView.OnMultiplayerClicked += HandleMultiplayer;
            _homeView.OnAchievementsClicked += () => _homeView.SetStatus("成就：占位");
            _homeView.OnSettingsClicked += () => _homeView.SetStatus("设置：占位");
            _homeView.OnExitClicked += HandleExit;

            _homeView.OnServerListBackClicked += HandleServerListBack;
            _homeView.OnRefreshServerListClicked += () => HandleRefreshServerList().Forget();
            _homeView.OnCreateRoomClicked += () => HandleCreateRoom().Forget();
            _homeView.OnInviteClicked += HandleInvite;
            _homeView.OnJoinRoomClicked += id => HandleJoinRoom(id).Forget();

            _homeView.OnRoomLeaveClicked += HandleRoomLeave;
            _homeView.OnRoomInviteClicked += HandleInvite;
            _homeView.OnRoomStartClicked += () => _homeView.SetRoomWaitingStatus("开始游戏：占位");

            if (_steam != null)
            {
                _steam.OnSteamError += msg =>
                {
                    _homeView.SetServerListStatus(msg);
                    _homeView.SetRoomWaitingStatus(msg);
                };
                _steam.OnLobbyMembersChanged += RefreshWaitingMembers;
                _steam.OnLobbyEntered += _ => RefreshWaitingMembers();
            }

            InitSteam();
            Debug.Log("[AppFlow] Start (Steam Lobby only for MP)");
            await GotoAsync(AppState.CheckUpdate, ct);
        }

        void InitSteam()
        {
            if (_steam == null)
            {
                _loginView.SetSteamMode(false, null);
                return;
            }
            if (_steam.Init())
                _loginView.SetSteamMode(true, _steam.PersonaName);
            else
                _loginView.SetSteamMode(false, null);
        }

        void OnUnauthorized()
        {
            if (_unauthorizedHandling) return;
            if (State == AppState.Login || State == AppState.Boot || State == AppState.CheckUpdate)
                return;
            _unauthorizedHandling = true;
            _auth.Logout();
            GotoAsync(AppState.Login).ContinueWith(() => { _unauthorizedHandling = false; }).Forget();
        }

        async UniTask GotoAsync(AppState next, CancellationToken ct = default)
        {
            State = next;
            Debug.Log($"[AppFlow] → {next}");
            switch (next)
            {
                case AppState.CheckUpdate:
                    await DoCheckUpdateAsync(ct);
                    break;
                case AppState.Login:
                    _homeView.Hide();
                    if (_steam != null && _steam.IsInitialized)
                        _loginView.SetSteamMode(true, _steam.PersonaName);
                    else
                        _loginView.SetSteamMode(false, null);
                    _loginView.Show();
                    _loginView.SetStatus(_steam != null && _steam.IsInitialized
                        ? "点击「进入游戏」"
                        : "Steam 未就绪，可使用开发登录");
                    _loginView.SetInteractable(true);
                    break;
                case AppState.Home:
                    _loginView.Hide();
                    await EnterHomeAsync(ct);
                    break;
            }
        }

        async UniTask DoCheckUpdateAsync(CancellationToken ct)
        {
            try
            {
                var (ok, resp, msg) = await _version.CheckVersionAsync(ct);
                if (!ok) Debug.LogWarning("[AppFlow] version check failed: " + msg);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AppFlow] version check: " + e.Message);
            }

            _auth.TryRestoreToken();
            if (!_auth.IsLoggedIn)
            {
                await GotoAsync(AppState.Login, ct);
                return;
            }
            var valid = await _auth.ValidateSessionAsync(ct);
            await GotoAsync(valid ? AppState.Home : AppState.Login, ct);
        }

        async void HandleSteamEnterClicked()
        {
            if (_steam == null || !_steam.IsInitialized)
            {
                _loginView.SetStatus("Steam 未初始化");
                return;
            }
            _loginView.SetInteractable(false);
            _loginView.SetStatus("Steam 登录中…");
            try
            {
                var ticket = _steam.GetAuthSessionTicketHex();
                var (ok, err) = await _auth.LoginWithSteamAsync(_steam.SteamId, ticket);
                if (!ok)
                {
                    _loginView.SetStatus(err);
                    _loginView.SetInteractable(true);
                    return;
                }
                await GotoAsync(AppState.Home);
            }
            catch (Exception e)
            {
                _loginView.SetStatus(e.Message);
                _loginView.SetInteractable(true);
            }
        }

        async void HandleDevLoginSubmitted(string username, string password)
        {
            _loginView.SetInteractable(false);
            _loginView.SetStatus("开发登录中…");
            try
            {
                var (ok, err) = await _auth.LoginAsync(username, password);
                if (!ok)
                {
                    _loginView.SetStatus(err);
                    _loginView.SetInteractable(true);
                    return;
                }
                await GotoAsync(AppState.Home);
            }
            catch (Exception e)
            {
                _loginView.SetStatus(e.Message);
                _loginView.SetInteractable(true);
            }
        }

        void HandleMultiplayer()
        {
            if (_steam == null || !_steam.IsInitialized)
            {
                _homeView.SetStatus("多人需要 Steam（P2P 房间走 Steam Lobby）");
                return;
            }
            _homeView.ShowRoomWaiting(false);
            _homeView.ShowServerList(true);
            _homeView.SetServerListStatus("拉取 Steam 房间列表…");
            HandleRefreshServerList().Forget();
        }

        void HandleServerListBack()
        {
            _homeView.ShowServerList(false);
            _homeView.SetStatus("");
        }

        void HandleExit()
        {
            HandleRoomLeave();
            _net?.Disconnect();
            _auth.Logout();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        async UniTask HandleRefreshServerList()
        {
            if (_steam == null || !_steam.IsInitialized)
            {
                _homeView.SetRoomList(Array.Empty<RoomListItem>());
                _homeView.SetServerListStatus("需要 Steam");
                return;
            }

            _homeView.SetServerListStatus("刷新 Steam Lobby 列表…");
            var items = await _steam.RequestLobbyListAsync();
            _homeView.SetRoomList(items ?? Array.Empty<RoomListItem>());
            int n = items?.Length ?? 0;
            _homeView.SetServerListStatus(n == 0
                ? "暂无公开房间 · 点击【创建】或等好友邀请"
                : $"Steam 公开房间 {n} 个");
        }

        async UniTaskVoid HandleCreateRoom()
        {
            if (_steam == null || !_steam.IsInitialized)
            {
                _homeView.SetServerListStatus("需要 Steam");
                return;
            }

            _homeView.SetServerListStatus("正在创建 Steam 房间…");
            var name = (_steam.PersonaName ?? "玩家") + " 的房间";
            var lobbyId = await _steam.CreateLobbyAsync(name, 4);
            if (lobbyId == 0)
            {
                _homeView.SetServerListStatus("创建失败");
                return;
            }

            _isRoomHost = true;
            // 传输层本机 Host（同网调试）；正式应对接 Steam Networking
            if (_net != null && !_net.IsConnected)
                await _net.StartHostAsync(9050);

            EnterRoomWaiting();
        }

        async UniTaskVoid HandleJoinRoom(string roomId)
        {
            if (_steam == null || !_steam.IsInitialized)
            {
                _homeView.SetServerListStatus("需要 Steam");
                return;
            }
            if (!ulong.TryParse(roomId, out var lobbyId) || lobbyId == 0)
            {
                _homeView.SetServerListStatus("无效房间 ID");
                return;
            }

            _homeView.SetServerListStatus("正在加入…");
            var ok = await _steam.JoinLobbyAsync(lobbyId);
            if (!ok)
            {
                _homeView.SetServerListStatus("加入失败");
                return;
            }

            _isRoomHost = false;
            // 正式：Steam P2P 连房主；同网调试可再补 Host 地址
            EnterRoomWaiting();
        }

        void EnterRoomWaiting()
        {
            _homeView.ShowServerList(false);
            _homeView.ShowRoomWaiting(true);
            RefreshWaitingMembers();
            _homeView.SetRoomWaitingStatus(_isRoomHost
                ? "你是房主 · 等待其他玩家（Steam Lobby）"
                : "已加入 · 等待房主开始");
        }

        void RefreshWaitingMembers()
        {
            if (_steam == null || _steam.CurrentLobbyId == 0) return;
            var members = _steam.GetLobbyMemberNames();
            var extra = $"人数 {_steam.LobbyMemberCount}/4";
            var lines = new string[(members?.Length ?? 0) + 1];
            if (members != null)
                Array.Copy(members, lines, members.Length);
            lines[lines.Length - 1] = extra;

            _homeView.SetRoomWaitingInfo(
                _steam.CurrentLobbyName,
                _steam.CurrentLobbyId.ToString(),
                lines,
                _isRoomHost);
        }

        void HandleInvite()
        {
            if (_steam == null || !_steam.IsInitialized)
            {
                _homeView.SetServerListStatus("需要 Steam");
                return;
            }
            if (_steam.CurrentLobbyId == 0)
            {
                _homeView.SetServerListStatus("请先【创建】房间再邀请");
                _homeView.SetRoomWaitingStatus("请先在房间内再邀请");
                return;
            }
            _steam.InviteFriendsOverlay();
            _homeView.SetRoomWaitingStatus("已打开 Steam 邀请");
            _homeView.SetServerListStatus("已打开 Steam 邀请");
        }

        void HandleRoomLeave()
        {
            _isRoomHost = false;
            _net?.Disconnect();
            _steam?.LeaveLobby();
            _homeView.ShowRoomWaiting(false);
            _homeView.ShowServerList(true);
            HandleRefreshServerList().Forget();
        }

        async UniTask EnterHomeAsync(CancellationToken ct)
        {
            _homeView.Show();
            _homeView.ShowRoomWaiting(false);
            _homeView.ShowServerList(false);
            _homeView.SetVersions("v" + _config.ClientVersionCode, _config.ResourceVersion);
            _homeView.SetStatus("拉取资料中…");
            if (_steam != null && _steam.IsInitialized)
                _homeView.SetUserName(_steam.PersonaName);

            try
            {
                var (ok, profile, err) = await _player.FetchProfileAsync(ct);
                if (!ok)
                {
                    _homeView.SetStatus("拉资料失败: " + err);
                    return;
                }
                _homeView.ShowProfile(profile);
            }
            catch (UnauthorizedException) { }
            catch (Exception e)
            {
                _homeView.SetStatus("拉资料异常: " + e.Message);
            }
        }
    }
}
