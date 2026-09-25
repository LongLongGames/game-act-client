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
using GameAct.Gameplay;
using GameAct.Les;

namespace GameAct.AppFlow
{
    /// <summary>
    /// 流程控制器：页面互斥 + 进局时序。
    /// 单机：不 StartHost/Connect，进图前 Disconnect，运行时无 LiteNet Poll。
    /// 多人：点开始时才并行启动网络与场景；失败回房间，禁止降级单机。
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
        readonly IConfirmDialog _dialog;

        bool _unauthorizedHandling;
        bool _handlingDisconnect;
        bool _isRoomHost;
        bool _gameStarting;
        string _pendingLevelName = "Map1";

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
            ApiConfig config = null,
            IConfirmDialog dialog = null)
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
            _dialog = dialog;
        }

        public async UniTask StartAsync(CancellationToken ct = default)
        {
            _http.Unauthorized += OnUnauthorized;

            _login.OnDevLoginSubmitted += HandleDevLoginSubmitted;
            _login.OnSteamEnterClicked += HandleSteamEnterClicked;

            _mainMenu.OnSinglePlayerClicked += () => StartGameAsync("Map1", SessionMode.Solo).Forget();
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
            _room.OnStartClicked += () =>
            {
                var mode = _isRoomHost ? SessionMode.Host : SessionMode.Client;
                StartGameAsync("Map1", mode).Forget();
            };

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

            if (_net != null)
                _net.OnDisconnected += HandleNetworkDisconnected;

            InitSteam();
            LoadSettingsPrefs();
            Debug.Log("[AppFlow] Start — SessionMode explicit, Solo zero-LiteNet runtime");
            await GotoAsync(AppState.CheckUpdate, ct);
        }

        // ─── 页面互斥 ───────────────────────────────────────

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

        // ─── 进局：Loading → 并行(Net+Scene) → Active → Gameplay → HUD → 关 Loading ──

        async UniTaskVoid StartGameAsync(string sceneName, SessionMode mode)
        {
            if (_gameStarting) return;
            _gameStarting = true;
            _pendingLevelName = sceneName;
            bool fromRoom = mode == SessionMode.Host || mode == SessionMode.Client;

            try
            {
                ShowOnlyLoading();
                _loading?.SetProgress(0f);
                _loading?.SetStatus(mode == SessionMode.Solo
                    ? $"正在加载 {sceneName}…"
                    : "正在准备网络与场景…");

                // Solo：先清残留网络，保证本局无 Host/Client
                if (mode == SessionMode.Solo)
                    _net?.Disconnect();

                var networkTask = EnsureGameplayNetworkAsync(mode);
                var sceneTask = LoadLevelSceneAsync(sceneName);

                var scene = await sceneTask;
                await networkTask;

                if (!scene.IsValid() || !scene.isLoaded)
                    throw new InvalidOperationException($"场景未正确加载：{sceneName}");

                _loading?.SetStatus("正在初始化玩法…");
                _loading?.SetProgress(0.92f);

                DisableBootCameraAndListener();
                SceneManager.SetActiveScene(scene);

                StartGameplay(sceneName, mode);

                State = AppState.Gameplay;
                _hud?.Show();
                _hud?.SetStatus(mode == SessionMode.Solo
                    ? "单机 · WASD 移动 · Shift 冲刺 · Space 跳"
                    : $"联机({mode}) · 已进入 {sceneName}");
                _loading?.SetProgress(1f);
                _loading?.SetStatus("进入游戏");
                await UniTask.Yield();
                _loading?.Hide();

                Debug.Log($"[AppFlow] Gameplay ready: scene={sceneName} mode={mode} " +
                          $"netRole={_net?.Role} connected={_net?.IsConnected}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AppFlow] StartGame failed: {e}");
                _loading?.SetStatus("进入游戏失败：" + e.Message);
                _loading?.SetProgress(0f);
                await UniTask.Delay(1200);

                // 关卡可能已 Additive 加载：失败时卸掉，避免堆场景
                await UnloadLevelIfLoadedAsync(sceneName);

                if (mode != SessionMode.Solo)
                    _net?.Disconnect();

                StopExistingGameplay();

                if (fromRoom && _steam != null && _steam.CurrentLobbyId != 0)
                {
                    ShowOnlyRoom();
                    RefreshWaitingMembers();
                    _room.SetStatus("进入游戏失败：" + e.Message);
                }
                else
                {
                    ShowOnlyMainMenu();
                    _mainMenu.SetStatus("进入游戏失败：" + e.Message);
                }

                State = AppState.Home;
            }
            finally
            {
                _gameStarting = false;
            }
        }

        async UniTask EnsureGameplayNetworkAsync(SessionMode mode)
        {
            // LES Transport：Solo 断网；Host 起 ServerEM+UDP；Client 从 Lobby 连 Host
            await LesNetworkGate.EnsureAsync(_net, _steam, mode, _loading);
        }

        async UniTask<Scene> LoadLevelSceneAsync(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                throw new ArgumentException("场景名为空", nameof(sceneName));

            var existing = SceneManager.GetSceneByName(sceneName);
            if (existing.IsValid() && existing.isLoaded)
            {
                _loading?.SetProgress(0.9f);
                return existing;
            }

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null)
                throw new InvalidOperationException($"场景 {sceneName} 不存在或未加入 Build Settings");

            op.allowSceneActivation = true;
            while (!op.isDone)
            {
                _loading?.SetProgress(Mathf.Clamp01(op.progress / 0.9f) * 0.9f);
                _loading?.SetStatus($"加载场景 {sceneName}… {(int)(Mathf.Clamp01(op.progress / 0.9f) * 100f)}%");
                await UniTask.Yield();
            }

            return SceneManager.GetSceneByName(sceneName);
        }

        async UniTask UnloadLevelIfLoadedAsync(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded) return;
            try
            {
                var op = SceneManager.UnloadSceneAsync(scene);
                if (op != null)
                {
                    while (!op.isDone)
                        await UniTask.Yield();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AppFlow] UnloadLevel: " + e.Message);
            }
        }

        void StartGameplay(string levelSceneName, SessionMode mode)
        {
            // Solo 不传 net；Host/Client 传会话（Client 已在 Ensure 校验过）
            INetSession gameplayNet = mode == SessionMode.Solo ? null : _net;

            var existing = UnityEngine.Object.FindFirstObjectByType<GameplayRunner>();
            if (existing != null)
            {
                if (existing.IsStarted)
                    existing.StopSession();
                existing.StartSession(gameplayNet, levelSceneName, mode);
                return;
            }

            var go = new GameObject("GameplayRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var runner = go.AddComponent<GameplayRunner>();
            runner.StartSession(gameplayNet, levelSceneName, mode);
        }

        void StopExistingGameplay()
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<GameplayRunner>();
            if (existing != null)
                existing.StopSession();
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

        /// <summary>启动时尝试一次；失败不阻塞，登录页可手动重试。</summary>
        void InitSteam()
        {
            TryEnsureSteamAndRefreshLoginUi(showStatus: false);
        }

        /// <summary>
        /// 手动触发：若尚未 Init 则再调一次 SteamAPI.Init。
        /// 无轮询；仅在进入 Login / 点击「进入游戏」时调用。
        /// </summary>
        bool TryEnsureSteamAndRefreshLoginUi(bool showStatus)
        {
            if (_steam == null)
            {
                _login.SetSteamMode(false, null);
                if (showStatus)
                    _login.SetStatus("无 Steam 服务，请使用开发登录");
                return false;
            }

            if (_steam.IsInitialized || _steam.Init())
            {
                _login.SetSteamMode(true, _steam.PersonaName);
                if (showStatus)
                    _login.SetStatus("点击「进入游戏」");
                return true;
            }

            _login.SetSteamMode(false, null);
            if (showStatus)
                _login.SetStatus("Steam 未就绪：请先启动 Steam 客户端，再点「进入游戏」重试");
            return false;
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
                    // 进入登录页再尝试一次（启动时 Steam 未开、此时已开的情况）
                    if (TryEnsureSteamAndRefreshLoginUi(showStatus: false))
                        _login.SetStatus("点击「进入游戏」");
                    else
                        _login.SetStatus("Steam 未就绪：启动 Steam 后点「进入游戏」即可重试，或使用开发登录");
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
            // 手动重试 Init：先开游戏再开 Steam 时，点一次即可，无需重启客户端
            if (!TryEnsureSteamAndRefreshLoginUi(showStatus: true))
                return;

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

            var mp = _config?.MpBaseUrl ?? "(null)";
            Debug.Log($"[AppFlow] DevLogin start user={username} MpBaseUrl={mp} url={mp}/api/v1/auth/login");

            try
            {
                var (ok, err) = await _auth.LoginAsync(username, password);
                Debug.LogError($"[AppFlow] DevLogin FAIL: {err}");
                if (!ok)
                {
                    _login.SetStatus(err);
                    _login.SetInteractable(true);
                    return;
                }
                Debug.Log("[AppFlow] DevLogin OK → Home");
                await GotoAsync(AppState.Home);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AppFlow] DevLogin exception: {e}");
                _login.SetStatus(e.Message);
                _login.SetInteractable(true);
            }
        }

        async UniTask EnterMainMenuAsync(CancellationToken ct)
        {
            // 回主菜单时确保无残留网络
            _net?.Disconnect();
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
            // 网络不在创建房间时启动；点开始再与场景并行 StartHost
            _net?.Disconnect();
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
            _net?.Disconnect();
            EnterRoomWaiting();
        }

        // ─── 房间 ───────────────────────────────────────────

        void EnterRoomWaiting()
        {
            ShowOnlyRoom();
            RefreshWaitingMembers();
            _room.SetStatus(_isRoomHost
                ? "你是房主 · 等待其他玩家（Steam Lobby）· 点开始再启 Host"
                : "已加入 · 等待房主开始（客户端连接后续接入）");
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
            StopExistingGameplay();
            ShowOnlyLobby();
            HandleRefreshLobby().Forget();
        }

        void HandleExit()
        {
            _isRoomHost = false;
            _net?.Disconnect();
            _steam?.LeaveLobby();
            StopExistingGameplay();
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

        // ─── 局内断线：确认框 → 回房间/大厅 ─────────────────

        /// <summary>
        /// 局内网络断开入口。Solo / 非 Gameplay / 进局中 忽略。
        /// </summary>
        void HandleNetworkDisconnected()
        {
            if (_handlingDisconnect) return;
            if (State != AppState.Gameplay) return;
            if (_gameStarting) return;

            _handlingDisconnect = true;
            HandleNetworkDisconnectedAsync().Forget();
        }

        async UniTaskVoid HandleNetworkDisconnectedAsync()
        {
            try
            {
                Debug.Log("[AppFlow] Network disconnected during Gameplay → dialog → Room/Lobby");

                StopExistingGameplay();
                _net?.Disconnect();

                var level = string.IsNullOrEmpty(_pendingLevelName) ? "Map1" : _pendingLevelName;
                await UnloadLevelIfLoadedAsync(level);

                bool stillInLobby = _steam != null && _steam.CurrentLobbyId != 0;

                string title = "连接已断开";
                string body = stillInLobby
                    ? (_isRoomHost
                        ? "网络已断开。点击确定返回房间，可再次开始游戏。"
                        : "与主机的连接已断开。点击确定返回房间，可等待房主再次开始或重新加入。")
                    : "连接已断开，当前房间已失效。点击确定返回大厅。";

                if (_dialog != null)
                    await _dialog.ShowAsync(title, body, "确定");
                else
                    Debug.LogWarning("[AppFlow] IConfirmDialog 未注入，断线仅用 status");

                if (stillInLobby)
                {
                    ShowOnlyRoom();
                    RefreshWaitingMembers();
                    _room.SetStatus(body);
                    _hud?.SetStatus(title);
                    State = AppState.Home;
                }
                else
                {
                    _isRoomHost = false;
                    ShowOnlyLobby();
                    _lobby.SetStatus("连接已断开 · 房间已失效，请重新创建或加入");
                    HandleRefreshLobby().Forget();
                    State = AppState.Home;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[AppFlow] HandleNetworkDisconnected failed: " + e);
                _isRoomHost = false;
                ShowOnlyMainMenu();
                _mainMenu.SetStatus("连接已断开");
                State = AppState.Home;
            }
            finally
            {
                _handlingDisconnect = false;
            }
        }


    }
}
