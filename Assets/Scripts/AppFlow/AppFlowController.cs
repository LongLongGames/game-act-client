using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameAct.Auth;
using GameAct.Network;
using GameAct.Services;
using GameAct.UI;
using GameAct.Steam;
using GameAct.Net;

namespace GameAct.AppFlow
{
    /// <summary>
    /// 流程控制器：持有各自独立的 View，负责页面切换与业务。
    /// 主菜单 / 大厅 / 房间 / 设置 完全解耦，互不嵌套。
    /// </summary>
    public class AppFlowController : IAppFlow
    {
        public AppState State { get; private set; } = AppState.Boot;

        readonly IVersionService _version;
        readonly IAuthService _auth;
        readonly IPlayerService _player;
        readonly IHttpClient _http;
        readonly ILoginView _login;
        readonly IMainMenuView _mainMenu;
        readonly ILobbyView _lobby;
        readonly IRoomView _room;
        readonly ISettingsView _settings;
        readonly ILoadingView _loading;
        readonly IGameHudView _hud;
        readonly ISteamService _steam;
        readonly INetSession _net;
        readonly ApiConfig _config;

        bool _unauthorizedHandling;
        bool _isRoomHost;

        float _bgmVolume = 80f;
        float _sfxVolume = 100f;
        int _resolutionIndex;
        int _aaIndex = 2;
        int _vsyncIndex;
        static readonly string[] ResolutionOptions = { "1920×1080", "1600×900", "1280×720", "1280×800", "1024×768" };
        static readonly string[] AaOptions = { "关闭", "2x", "4x", "8x" };
        static readonly int[] AaValues = { 0, 2, 4, 8 };

        public AppFlowController(
            IVersionService version,
            IAuthService auth,
            IPlayerService player,
            IHttpClient http,
            ILoginView login,
            IMainMenuView mainMenu,
            ILobbyView lobby,
            IRoomView room,
            ISettingsView settings,
            ILoadingView loading = null,
            IGameHudView hud = null,
            ISteamService steam = null,
            INetSession net = null,
            ApiConfig config = null)
        {
            _version = version;
            _auth = auth;
            _player = player;
            _http = http;
            _login = login;
            _mainMenu = mainMenu;
            _lobby = lobby;
            _room = room;
            _settings = settings;
            _loading = loading;
            _hud = hud;
            _steam = steam;
            _net = net;
            _config = config ?? new ApiConfig();
        }

        public async UniTask StartAsync(CancellationToken ct = default)
        {
            _http.Unauthorized += OnUnauthorized;

            _login.OnDevLoginSubmitted += HandleDevLoginSubmitted;
            _login.OnSteamEnterClicked += HandleSteamEnterClicked;

            _mainMenu.OnSinglePlayerClicked += () => StartGameAsync("Map1").Forget();
            _mainMenu.OnMultiplayerClicked += HandleMultiplayer;
            _mainMenu.OnAchievementsClicked += () => _mainMenu.SetStatus("成就：占位（独立界面后续接）");
            _mainMenu.OnSettingsClicked += HandleOpenSettings;
            _mainMenu.OnExitClicked += HandleExit;

            _lobby.OnBackClicked += HandleLobbyBack;
            _lobby.OnRefreshClicked += () => HandleRefreshLobby().Forget();
            _lobby.OnCreateRoomClicked += () => HandleCreateRoom().Forget();
            _lobby.OnJoinRoomClicked += id => HandleJoinRoom(id).Forget();

            _room.OnLeaveClicked += HandleRoomLeave;
            _room.OnInviteClicked += HandleInvite;
            _room.OnStartClicked += () => StartGameAsync("Map1").Forget();

            if (_hud != null)
            {
                _hud.OnBagClicked += () => _hud.SetStatus("背包：占位");
                _hud.OnMailClicked += () => _hud.SetStatus("邮件：占位");
                _hud.OnQuestClicked += () => _hud.SetStatus("任务：占位");
                _hud.OnSkillClicked += () => _hud.SetStatus("技能：占位");
                _hud.OnMapClicked += () => _hud.SetStatus("地图：占位");
            }

            _settings.OnBackClicked += HandleSettingsBack;
            _settings.OnDisplayDefaultsClicked += HandleDisplayDefaults;
            _settings.OnAudioDefaultsClicked += HandleAudioDefaults;
            _settings.OnControlsDefaultsClicked += HandleControlsDefaults;
            _settings.OnFullscreenChanged += HandleFullscreen;
            _settings.OnResolutionIndexChanged += HandleResolution;
            _settings.OnAntiAliasingIndexChanged += HandleAntiAliasing;
            _settings.OnVSyncIndexChanged += HandleVSync;
            _settings.OnBgmVolumeChanged += v => { _bgmVolume = v; ApplyAudio(); };
            _settings.OnSfxVolumeChanged += v => { _sfxVolume = v; ApplyAudio(); };

            if (_steam != null)
            {
                _steam.OnSteamError += msg =>
                {
                    _lobby.SetStatus(msg);
                    _room.SetStatus(msg);
                };
                _steam.OnLobbyMembersChanged += RefreshWaitingMembers;
                _steam.OnLobbyEntered += _ => RefreshWaitingMembers();
            }

            InitSteam();
            LoadSettingsPrefs();
            Debug.Log("[AppFlow] Start — views decoupled");
            await GotoAsync(AppState.CheckUpdate, ct);
        }

        // ─── 页面互斥切换 ───────────────────────────────────

        void ShowOnlyMainMenu()
        {
            _login.Hide();
            _lobby.Hide();
            _room.Hide();
            _settings.Hide();
            _loading?.Hide();
            _hud?.Hide();
            _mainMenu.Show();
        }

        void ShowOnlyLobby()
        {
            _login.Hide();
            _mainMenu.Hide();
            _room.Hide();
            _settings.Hide();
            _loading?.Hide();
            _hud?.Hide();
            _lobby.Show();
        }

        void ShowOnlyRoom()
        {
            _login.Hide();
            _mainMenu.Hide();
            _lobby.Hide();
            _settings.Hide();
            _loading?.Hide();
            _hud?.Hide();
            _room.Show();
        }

        void ShowOnlySettings()
        {
            _login.Hide();
            _mainMenu.Hide();
            _lobby.Hide();
            _room.Hide();
            _loading?.Hide();
            _hud?.Hide();
            _settings.Show();
        }

        void ShowOnlyLogin()
        {
            _mainMenu.Hide();
            _lobby.Hide();
            _room.Hide();
            _settings.Hide();
            _loading?.Hide();
            _hud?.Hide();
            _login.Show();
        }

        void ShowOnlyLoading()
        {
            _login.Hide();
            _mainMenu.Hide();
            _lobby.Hide();
            _room.Hide();
            _settings.Hide();
            _hud?.Hide();
            _loading?.Show();
        }

        /// <summary>
        /// 开始游戏：显示 Loading，异步 Additive 加载指定场景（默认 Map1）。
        /// </summary>
        async UniTaskVoid StartGameAsync(string sceneName = "Map1")
        {
            ShowOnlyLoading();
            _loading?.SetStatus($"正在加载 {sceneName}…");
            _loading?.SetProgress(0f);

            // 确保场景已加入 Build Settings；若未加入则尝试按名加载
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                _loading?.SetStatus($"加载失败：场景 {sceneName} 不存在或未加入 Build Settings");
                Debug.LogError($"[AppFlow] LoadSceneAsync failed: {sceneName}");
                await UniTask.Delay(2000);
                ShowOnlyMainMenu();
                _mainMenu.SetStatus($"加载失败: {sceneName}");
                return;
            }

            op.allowSceneActivation = true;
            while (!op.isDone)
            {
                // Unity 进度 0~0.9 为加载，0.9~1 为激活
                float p = Mathf.Clamp01(op.progress / 0.9f);
                _loading?.SetProgress(p);
                _loading?.SetStatus($"加载中… {(int)(p * 100)}%");
                await UniTask.Yield();
            }

            _loading?.SetProgress(1f);
            _loading?.SetStatus("加载完成");
            await UniTask.Delay(300);
            _loading?.Hide();

            // 进游戏：显示 HUD，关掉 Boot 相机/Listener（避免双 Camera / 双 AudioListener）
            DisableBootCameraAndListener();
            _hud?.Show();
            _hud?.SetStatus("已进入 " + sceneName);
            Debug.Log($"[AppFlow] Additive loaded: {sceneName}");
        }

        void DisableBootCameraAndListener()
        {
            foreach (var cam in Camera.allCameras)
            {
                if (cam != null && cam.gameObject.scene.name == "Boot")
                    cam.enabled = false;
            }
            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            foreach (var al in listeners)
            {
                if (al != null && al.gameObject.scene.name == "Boot")
                    al.enabled = false;
            }
        }

        // ─── Boot / Login / Home ────────────────────────────

        void InitSteam()
        {
            if (_steam == null)
            {
                _login.SetSteamMode(false, null);
                return;
            }
            if (_steam.Init())
                _login.SetSteamMode(true, _steam.PersonaName);
            else
                _login.SetSteamMode(false, null);
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
                    ShowOnlyLogin();
                    if (_steam != null && _steam.IsInitialized)
                        _login.SetSteamMode(true, _steam.PersonaName);
                    else
                        _login.SetSteamMode(false, null);
                    _login.SetStatus(_steam != null && _steam.IsInitialized
                        ? "点击「进入游戏」"
                        : "Steam 未就绪，可使用开发登录");
                    _login.SetInteractable(true);
                    break;
                case AppState.Home:
                    await EnterMainMenuAsync(ct);
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
                _login.SetStatus("Steam 未初始化");
                return;
            }
            _login.SetInteractable(false);
            _login.SetStatus("Steam 登录中…");
            try
            {
                var ticket = _steam.GetAuthSessionTicketHex();
                var (ok, err) = await _auth.LoginWithSteamAsync(_steam.SteamId, ticket);
                if (!ok)
                {
                    _login.SetStatus(err);
                    _login.SetInteractable(true);
                    return;
                }
                await GotoAsync(AppState.Home);
            }
            catch (Exception e)
            {
                _login.SetStatus(e.Message);
                _login.SetInteractable(true);
            }
        }

        async void HandleDevLoginSubmitted(string username, string password)
        {
            _login.SetInteractable(false);
            _login.SetStatus("开发登录中…");
            try
            {
                var (ok, err) = await _auth.LoginAsync(username, password);
                if (!ok)
                {
                    _login.SetStatus(err);
                    _login.SetInteractable(true);
                    return;
                }
                await GotoAsync(AppState.Home);
            }
            catch (Exception e)
            {
                _login.SetStatus(e.Message);
                _login.SetInteractable(true);
            }
        }

        async UniTask EnterMainMenuAsync(CancellationToken ct)
        {
            ShowOnlyMainMenu();
            _mainMenu.SetVersions("v" + _config.ClientVersionCode, _config.ResourceVersion);
            _mainMenu.SetStatus("拉取资料中…");
            if (_steam != null && _steam.IsInitialized)
                _mainMenu.SetUserName(_steam.PersonaName);

            try
            {
                var (ok, profile, err) = await _player.FetchProfileAsync(ct);
                if (!ok)
                {
                    _mainMenu.SetStatus("拉资料失败: " + err);
                    return;
                }
                _mainMenu.ShowProfile(profile);
            }
            catch (UnauthorizedException) { }
            catch (Exception e)
            {
                _mainMenu.SetStatus("拉资料异常: " + e.Message);
            }
        }

        // ─── 大厅 ───────────────────────────────────────────

        void HandleMultiplayer()
        {
            if (_steam == null || !_steam.IsInitialized)
            {
                _mainMenu.SetStatus("多人需要 Steam（P2P 房间走 Steam Lobby）");
                return;
            }
            ShowOnlyLobby();
            _lobby.SetStatus("拉取 Steam 房间列表…");
            HandleRefreshLobby().Forget();
        }

        void HandleLobbyBack()
        {
            ShowOnlyMainMenu();
            _mainMenu.SetStatus("");
        }

        async UniTask HandleRefreshLobby()
        {
            if (_steam == null || !_steam.IsInitialized)
            {
                _lobby.SetRoomList(Array.Empty<RoomListItem>());
                _lobby.SetStatus("需要 Steam");
                return;
            }

            _lobby.SetStatus("刷新 Steam Lobby 列表…");
            var items = await _steam.RequestLobbyListAsync();
            _lobby.SetRoomList(items ?? Array.Empty<RoomListItem>());
            int n = items?.Length ?? 0;
            _lobby.SetStatus(n == 0
                ? "暂无公开房间 · 点击【创建】或等好友邀请"
                : $"Steam 公开房间 {n} 个");
        }

        async UniTaskVoid HandleCreateRoom()
        {
            if (_steam == null || !_steam.IsInitialized)
            {
                _lobby.SetStatus("需要 Steam");
                return;
            }

            _lobby.SetStatus("正在创建 Steam 房间…");
            var name = (_steam.PersonaName ?? "玩家") + " 的房间";
            var lobbyId = await _steam.CreateLobbyAsync(name, 4);
            if (lobbyId == 0)
            {
                _lobby.SetStatus("创建失败");
                return;
            }

            _isRoomHost = true;
            if (_net != null && !_net.IsConnected)
                await _net.StartHostAsync(9050);

            EnterRoomWaiting();
        }

        async UniTaskVoid HandleJoinRoom(string roomId)
        {
            if (_steam == null || !_steam.IsInitialized)
            {
                _lobby.SetStatus("需要 Steam");
                return;
            }
            if (!ulong.TryParse(roomId, out var lobbyId) || lobbyId == 0)
            {
                _lobby.SetStatus("无效房间 ID");
                return;
            }

            _lobby.SetStatus("正在加入…");
            var ok = await _steam.JoinLobbyAsync(lobbyId);
            if (!ok)
            {
                _lobby.SetStatus("加入失败");
                return;
            }

            _isRoomHost = false;
            EnterRoomWaiting();
        }

        // ─── 房间 ───────────────────────────────────────────

        void EnterRoomWaiting()
        {
            ShowOnlyRoom();
            RefreshWaitingMembers();
            _room.SetStatus(_isRoomHost
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

            _room.SetInfo(
                _steam.CurrentLobbyName,
                _steam.CurrentLobbyId.ToString(),
                lines,
                _isRoomHost);
        }

        void HandleInvite()
        {
            if (_steam == null || !_steam.IsInitialized)
            {
                _room.SetStatus("需要 Steam");
                return;
            }
            if (_steam.CurrentLobbyId == 0)
            {
                _room.SetStatus("请先在房间内再邀请");
                return;
            }
            _steam.InviteFriendsOverlay();
            _room.SetStatus("已打开 Steam 邀请");
        }

        void HandleRoomLeave()
        {
            _isRoomHost = false;
            _net?.Disconnect();
            _steam?.LeaveLobby();
            ShowOnlyLobby();
            HandleRefreshLobby().Forget();
        }

        void HandleExit()
        {
            _isRoomHost = false;
            _net?.Disconnect();
            _steam?.LeaveLobby();
            _auth.Logout();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ─── 设置 ───────────────────────────────────────────

        void HandleOpenSettings()
        {
            ShowOnlySettings();
            PushSettingsToView();
            _settings.SetStatus("");
        }

        void HandleSettingsBack()
        {
            SaveSettingsPrefs();
            ShowOnlyMainMenu();
            _mainMenu.SetStatus("");
        }

        void PushSettingsToView()
        {
            _settings.SetDisplay(
                Screen.fullScreen,
                _resolutionIndex,
                ResolutionOptions,
                _aaIndex,
                AaOptions,
                _vsyncIndex);
            _settings.SetAudio(_bgmVolume, _sfxVolume);
        }

        void HandleDisplayDefaults()
        {
            _resolutionIndex = 0;
            _aaIndex = 2;
            _vsyncIndex = QualitySettings.vSyncCount > 0 ? 1 : 0;
            Screen.fullScreen = true;
            ApplyResolution();
            ApplyAntiAliasing();
            ApplyVSync();
            PushSettingsToView();
            _settings.SetStatus("显示已恢复默认");
        }

        void HandleAudioDefaults()
        {
            _bgmVolume = 80f;
            _sfxVolume = 100f;
            ApplyAudio();
            _settings.SetAudio(_bgmVolume, _sfxVolume);
            _settings.SetStatus("音频已恢复默认");
        }

        void HandleControlsDefaults()
        {
            _settings.SetStatus("键位已恢复默认（占位）");
        }

        void HandleFullscreen(bool on)
        {
            Screen.fullScreen = on;
            _settings.SetStatus(on ? "已开启全屏" : "已关闭全屏");
        }

        void HandleResolution(int index)
        {
            _resolutionIndex = Mathf.Clamp(index, 0, ResolutionOptions.Length - 1);
            ApplyResolution();
            _settings.SetStatus("分辨率: " + ResolutionOptions[_resolutionIndex]);
        }

        void HandleAntiAliasing(int index)
        {
            _aaIndex = Mathf.Clamp(index, 0, AaValues.Length - 1);
            ApplyAntiAliasing();
            _settings.SetStatus("抗锯齿: " + AaOptions[_aaIndex]);
        }

        void HandleVSync(int index)
        {
            _vsyncIndex = index > 0 ? 1 : 0;
            ApplyVSync();
            _settings.SetStatus(_vsyncIndex > 0 ? "垂直同步: 开启" : "垂直同步: 关闭");
        }

        void ApplyResolution()
        {
            var s = ResolutionOptions[_resolutionIndex];
            var parts = s.Split('×', 'x', 'X');
            if (parts.Length >= 2 &&
                int.TryParse(parts[0].Trim(), out var w) &&
                int.TryParse(parts[1].Trim(), out var h))
            {
                Screen.SetResolution(w, h, Screen.fullScreen);
            }
        }

        void ApplyAntiAliasing()
        {
            QualitySettings.antiAliasing = AaValues[_aaIndex];
        }

        void ApplyVSync()
        {
            QualitySettings.vSyncCount = _vsyncIndex;
        }

        void ApplyAudio()
        {
            AudioListener.volume = Mathf.Clamp01((_bgmVolume + _sfxVolume) * 0.005f);
        }

        void LoadSettingsPrefs()
        {
            _bgmVolume = PlayerPrefs.GetFloat("set_bgm", 80f);
            _sfxVolume = PlayerPrefs.GetFloat("set_sfx", 100f);
            _resolutionIndex = PlayerPrefs.GetInt("set_res", 0);
            _aaIndex = PlayerPrefs.GetInt("set_aa", 2);
            _vsyncIndex = PlayerPrefs.GetInt("set_vsync", QualitySettings.vSyncCount > 0 ? 1 : 0);
            ApplyAntiAliasing();
            ApplyVSync();
            ApplyAudio();
        }

        void SaveSettingsPrefs()
        {
            PlayerPrefs.SetFloat("set_bgm", _bgmVolume);
            PlayerPrefs.SetFloat("set_sfx", _sfxVolume);
            PlayerPrefs.SetInt("set_res", _resolutionIndex);
            PlayerPrefs.SetInt("set_aa", _aaIndex);
            PlayerPrefs.SetInt("set_vsync", _vsyncIndex);
            PlayerPrefs.Save();
        }
    }
}
