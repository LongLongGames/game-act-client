using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using GameAct.Network;
using GameAct.Services;

namespace GameAct.Auth
{
    /// <summary>
    /// 登录 / 会话校验 / 登出。严格遵守 ADR-0004。
    /// 正式：LoginWithSteamAsync；开发：LoginAsync 官方账号。
    /// </summary>
    public class AuthService : IAuthService
    {
        readonly IHttpClient _http;
        readonly ApiConfig _config;
        readonly ITokenStore _tokenStore;

        public bool IsLoggedIn => !string.IsNullOrEmpty(_http.AccessToken);

        public AuthService(IHttpClient http, ApiConfig config, ITokenStore tokenStore)
        {
            _http = http;
            _config = config;
            _tokenStore = tokenStore;
        }

        public UniTask<(bool ok, string error)> LoginAsync(string username, string password, CancellationToken ct = default)
        {
            var body = new LoginRequest
            {
                provider = "official",
                app_id = _config.AppId,
                device_id = SystemInfo.deviceUniqueIdentifier,
                auth_payload = new LoginAuthPayload
                {
                    username = username,
                    password = password
                }
            };
            return PostLoginAsync(body, ct);
        }

        public UniTask<(bool ok, string error)> LoginWithSteamAsync(ulong steamId, string sessionTicketHex, CancellationToken ct = default)
        {
            if (steamId == 0)
                return UniTask.FromResult<(bool, string)>((false, "SteamId 无效"));

            var body = new LoginRequest
            {
                provider = "steam",
                app_id = _config.AppId,
                device_id = SystemInfo.deviceUniqueIdentifier,
                auth_payload = new LoginAuthPayload
                {
                    steam_id = steamId.ToString(),
                    session_ticket = sessionTicketHex ?? ""
                }
            };
            return PostLoginAsync(body, ct);
        }

        async UniTask<(bool ok, string error)> PostLoginAsync(LoginRequest body, CancellationToken ct)
        {
            var json = JsonUtility.ToJson(body);
            var url = _config.MpBaseUrl + "/api/v1/auth/login";
            Debug.Log($"[Auth] POST {url} provider={body.provider}");

            try
            {
                var text = await _http.PostJsonAsync(url, json, auth: false, ct);
                var resp = JsonUtility.FromJson<LoginResponse>(text);
                if (resp == null || string.IsNullOrEmpty(resp.access_token))
                    return (false, "login failed: empty token");

                _http.AccessToken = resp.access_token;
                _tokenStore.SetAccessToken(resp.access_token);
                Debug.Log($"[Auth] Login OK provider={body.provider}");
                return (true, null);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Auth] POST {url} failed: {e.Message}");
                var msg = e.Message;
                if (string.IsNullOrEmpty(msg))
                    msg = e.GetType().Name;
                if (e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message))
                    msg = msg + " | " + e.InnerException.Message;
                return (false, msg);
            }
        }

        public void TryRestoreToken()
        {
            var t = _tokenStore.GetAccessToken();
            if (!string.IsNullOrEmpty(t))
                _http.AccessToken = t;
        }

        public async UniTask<bool> ValidateSessionAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_http.AccessToken))
                return false;

            var url = $"{_config.GameBaseUrl}/api/v1/user/profile?game_id={_config.GameId}";
            try
            {
                await _http.GetAsync(url, auth: true, ct);
                Debug.Log("[Auth] ValidateSession OK");
                return true;
            }
            catch (UnauthorizedException)
            {
                Debug.LogWarning("[Auth] ValidateSession 401 → Logout");
                Logout();
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Auth] ValidateSession failed: " + e.Message);
                return false;
            }
        }

        public void Logout()
        {
            _http.AccessToken = null;
            _tokenStore.ClearToken();
            Debug.Log("[Auth] Logout, token cleared");
        }
    }
}
