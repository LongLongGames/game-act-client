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
    /// 全局流程：CheckUpdate →（校验 Token）→ Login / Home
    /// 401：HTTP 层触发 → 强制登出回 Login。符合 ADR-0004。
    /// P1：Home 挂 Steam Lobby + LiteNet Host/Join。
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

        bool _unauthorizedHandling;

        public AppFlowController(
            IVersionService version,
            IAuthService auth,
            IPlayerService player,
            IHttpClient http,
            ILoginView loginView,
            IHomeView homeView,
            ISteamService steam = null,
            INetSession net = null)
        {
            _version = version;
            _auth = auth;
            _player = player;
            _http = http;
            _loginView = loginView;
            _homeView = homeView;
            _steam = steam;
            _net = net;
        }

        public async UniTask StartAsync(CancellationToken ct = default)
        {
            _http.Unauthorized += OnUnauthorized;
            _loginView.OnLoginSubmitted += HandleLoginSubmitted;
            _homeView.OnLogoutClicked += HandleLogoutClicked;
            _homeView.OnCreateLobbyClicked += HandleCreateLobby;
            _homeView.OnInviteFriendsClicked += HandleInviteFriends;
            _homeView.OnStartHostClicked += HandleStartHost;
            _homeView.OnConnectLocalClicked += HandleConnectLocal;
            _homeView.OnDisconnectNetClicked += HandleDisconnectNet;

            if (_steam != null)
            {
                _steam.OnLobbyCreated += id =>
                {
                    _homeView.SetLobbyId(id);
                    _homeView.SetSteamStatus($"Lobby 已创建 members={_steam.LobbyMemberCount}");
                };
                _steam.OnLobbyEntered += id =>
                {
                    _homeView.SetLobbyId(id);
                    _homeView.SetSteamStatus($"已进入 Lobby members={_steam.LobbyMemberCount}");
                };
                _steam.OnSteamError += msg => _homeView.SetSteamStatus(msg);
            }

            if (_net != null)
            {
                _net.OnLog += msg => _homeView.SetNetStatus(msg);
                _net.OnConnected += () => _homeView.SetNetStatus(_net.StatusText);
                _net.OnDisconnected += () => _homeView.SetNetStatus(_net.StatusText);
            }

            InitSteam();

            Debug.Log("[AppFlow] Start");
            await GotoAsync(AppState.CheckUpdate, ct);
        }

        void InitSteam()
        {
            if (_steam == null)
            {
                _homeView.SetSteamStatus("未注入 SteamService");
                return;
            }

            if (_steam.Init())
            {
                _homeView.SetSteamStatus($"OK  {_steam.PersonaName} ({_steam.SteamId})");
            }
            else
            {
                _homeView.SetSteamStatus("未就绪（需启动 Steam 客户端）");
            }
        }

        void OnUnauthorized()
        {
            if (_unauthorizedHandling) return;
            if (State == AppState.Login || State == AppState.Boot || State == AppState.CheckUpdate)
                return;

            _unauthorizedHandling = true;
            Debug.LogWarning("[AppFlow] 401 → force Logout + Login");
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
                    _loginView.Show();
                    _loginView.SetStatus("请登录");
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
                if (!ok)
                    Debug.LogWarning("[AppFlow] version check failed: " + msg);
                else if (resp != null && resp.force_update)
                    Debug.LogWarning("[AppFlow] force update required, latest=" + resp.latest_client_version);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AppFlow] version check exception: " + e.Message);
            }

            _auth.TryRestoreToken();
            if (!_auth.IsLoggedIn)
            {
                await GotoAsync(AppState.Login, ct);
                return;
            }

            var valid = await _auth.ValidateSessionAsync(ct);
            if (valid)
                await GotoAsync(AppState.Home, ct);
            else
                await GotoAsync(AppState.Login, ct);
        }

        async void HandleLoginSubmitted(string username, string password)
        {
            _loginView.SetInteractable(false);
            _loginView.SetStatus("登录中…");
            try
            {
                var (ok, err) = await _auth.LoginAsync(username, password);
                if (!ok)
                {
                    _loginView.SetStatus(FormatLoginError(err));
                    _loginView.SetInteractable(true);
                    return;
                }

                await GotoAsync(AppState.Home);
            }
            catch (Exception e)
            {
                _loginView.SetStatus("登录异常: " + e.Message);
                _loginView.SetInteractable(true);
            }
        }

        void HandleLogoutClicked()
        {
            _net?.Disconnect();
            _steam?.LeaveLobby();
            _auth.Logout();
            GotoAsync(AppState.Login).Forget();
        }

        async void HandleCreateLobby()
        {
            if (_steam == null || !_steam.IsInitialized)
            {
                _homeView.SetSteamStatus("Steam 未初始化");
                return;
            }
            _homeView.SetSteamStatus("创建 Lobby…");
            var id = await _steam.CreateLobbyAsync(4);
            if (id == 0)
                _homeView.SetSteamStatus("创建失败");
        }

        void HandleInviteFriends()
        {
            if (_steam == null || !_steam.IsInitialized)
            {
                _homeView.SetSteamStatus("Steam 未初始化");
                return;
            }
            _steam.InviteFriendsOverlay();
            _homeView.SetSteamStatus("已打开邀请 Overlay");
        }

        async void HandleStartHost()
        {
            if (_net == null)
            {
                _homeView.SetNetStatus("Net 未注入");
                return;
            }
            _homeView.SetNetStatus("启动 Host…");
            var ok = await _net.StartHostAsync(9050);
            _homeView.SetNetStatus(ok ? _net.StatusText : "Host 失败");
        }

        async void HandleConnectLocal()
        {
            if (_net == null)
            {
                _homeView.SetNetStatus("Net 未注入");
                return;
            }
            _homeView.SetNetStatus("连接 127.0.0.1:9050…");
            var ok = await _net.ConnectAsync("127.0.0.1", 9050);
            _homeView.SetNetStatus(ok ? _net.StatusText : "连接失败");
        }

        void HandleDisconnectNet()
        {
            _net?.Disconnect();
            _homeView.SetNetStatus("已断开");
        }

        async UniTask EnterHomeAsync(CancellationToken ct)
        {
            _homeView.Show();
            _homeView.SetStatus("拉取资料中…");

            if (_steam != null && _steam.IsInitialized)
                _homeView.SetSteamStatus($"OK  {_steam.PersonaName}");
            if (_net != null)
                _homeView.SetNetStatus(_net.StatusText);

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
            catch (UnauthorizedException)
            {
            }
            catch (Exception e)
            {
                _homeView.SetStatus("拉资料异常: " + e.Message);
            }
        }

        static string FormatLoginError(string err)
        {
            if (string.IsNullOrEmpty(err))
                return "登录失败，请稍后重试。";
            if (err.IndexOf("Cannot connect", StringComparison.OrdinalIgnoreCase) >= 0
                || err.IndexOf("Connection refused", StringComparison.OrdinalIgnoreCase) >= 0
                || err.IndexOf("Failed to connect", StringComparison.OrdinalIgnoreCase) >= 0)
                return "无法连接服务器，请确认 MP(11080) 与 game-act-server(13280) 已启动。\n\n详情：" + err;
            return "登录失败：" + err;
        }
    }
}
