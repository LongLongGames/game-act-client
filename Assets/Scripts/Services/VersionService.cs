using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using GameAct.Network;

namespace GameAct.Services
{
    public class VersionService : IVersionService
    {
        readonly IHttpClient _http;
        readonly ApiConfig _config;

        public VersionService(IHttpClient http, ApiConfig config)
        {
            _http = http;
            _config = config;
        }

        public async UniTask<(bool ok, VersionCheckResponse resp, string error)> CheckVersionAsync(CancellationToken ct = default)
        {
            var url =
                $"{_config.GameBaseUrl}/api/v1/game/version-check" +
                $"?game_id={_config.GameId}" +
                $"&channel={_config.Channel}" +
                $"&platform={_config.Platform}" +
                $"&region={_config.Region}" +
                $"&client_version_code={_config.ClientVersionCode}" +
                $"&resource_version={UnityEngine.Networking.UnityWebRequest.EscapeURL(_config.ResourceVersion)}";

            try
            {
                // 公开接口，无需 JWT
                var text = await _http.GetAsync(url, auth: false, ct);
                var resp = JsonUtility.FromJson<VersionCheckResponse>(text);
                if (resp == null)
                    return (false, null, "version-check: empty response");
                Debug.Log($"[Version] force={resp.force_update} optional={resp.optional_update} latest={resp.latest_client_version}");
                return (true, resp, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Version] Check failed: " + e.Message);
                return (false, null, e.Message);
            }
        }
    }
}
