using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using GameAct.Auth;
using GameAct.Network;
using GameAct.Services;
using GameAct.UI;

namespace GameAct.AppFlow
{
    /// <summary>
    /// 全局流程：CheckUpdate →（校验 Token）→ Login / Home
    /// 401：HTTP 层触发 → 强制登出回 Login。符合 ADR-0004。
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

        bool _unauthorizedHandling;

        public AppFlowController(
            IVersionService version,
            IAuthService auth,
            IPlayerService player,
            IHttpClient http,
            ILoginView loginView,
            IHomeView homeView)
        {
            _version = version;
            _auth = auth;
            _player = player;
            _http = http;
            _loginView = loginView;
            _homeView = homeView;
        }

        public async UniTask StartAsync(CancellationToken ct = default)
        {
            _http.Unauthorized += OnUnauthorized;
            _loginView.OnLoginSubmitted += HandleLoginSubmitted;
            _homeView.OnLogoutClicked += HandleLogoutClicked;

            Debug.Log("[AppFlow] Start");
            await GotoAsync(AppState.CheckUpdate, ct);
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
                    break;
                case AppState.Home:
                    _loginView.Hide();
                    await EnterHomeAsync(ct);
                    break;
                case AppState.Error:
                    _loginView.Hide();
                    _homeView.Hide();
                    Debug.LogError("[AppFlow] Error state");
                    break;
            }
        }

        async UniTask DoCheckUpdateAsync(CancellationToken ct)
        {
            _loginView.Show();
            _loginView.SetStatus("检查版本中…");

            var (ok, resp, err) = await _version.CheckVersionAsync(ct);
            if (!ok)
            {
                // 版本检查失败不阻塞联调：继续走登录（本地开发常见）
                Debug.LogWarning("[AppFlow] version-check failed, continue: " + err);
                _loginView.SetStatus("版本检查失败（可继续联调）: " + err);
            }
            else if (resp != null && resp.force_update)
            {
                _loginView.SetStatus($"强制更新: {resp.message}\n最新: {resp.latest_client_version}\n{resp.download_url}");
                State = AppState.Error;
                return;
            }
            else if (resp != null && resp.optional_update)
            {
                Debug.Log($"[AppFlow] optional update available: {resp.latest_client_version}");
            }

            // 恢复 Token → 探活
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
            _auth.Logout();
            GotoAsync(AppState.Login).Forget();
        }

        async UniTask EnterHomeAsync(CancellationToken ct)
        {
            _homeView.Show();
            _homeView.SetStatus("拉取资料中…");

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
                // OnUnauthorized 已处理
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
