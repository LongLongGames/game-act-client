using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using GameAct.Network;

namespace GameAct.Services
{
    public class PlayerService : IPlayerService
    {
        readonly IHttpClient _http;
        readonly ApiConfig _config;

        public PlayerProfile Profile { get; private set; }

        public PlayerService(IHttpClient http, ApiConfig config)
        {
            _http = http;
            _config = config;
        }

        public async UniTask<(bool ok, PlayerProfile profile, string error)> FetchProfileAsync(CancellationToken ct = default)
        {
            var url = $"{_config.GameBaseUrl}/api/v1/user/profile?game_id={_config.GameId}";
            try
            {
                var text = await _http.GetAsync(url, auth: true, ct);
                var profile = JsonUtility.FromJson<PlayerProfile>(text);
                if (profile == null)
                    return (false, null, "empty profile");
                Profile = profile;
                Debug.Log($"[Player] profile ok nickname={profile.nickname} level={profile.level}");
                return (true, profile, null);
            }
            catch (UnauthorizedException)
            {
                throw; // 交给 AppFlow 统一处理
            }
            catch (Exception e)
            {
                return (false, null, e.Message);
            }
        }
    }
}
