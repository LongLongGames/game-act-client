using System;
using System.IO;
using UnityEngine;

namespace GameAct.Network
{
    /// <summary>
    /// 统一加载客户端配置，优先级：
    /// 1) 可执行文件旁 client_config.json（打好包后可直接改，无需重编）
    /// 2) StreamingAssets/client_config.json（随包带出的默认）
    /// 3) 代码内 ApiConfig 默认值
    ///
    /// 局域网联调：把 mpBaseUrl / gameBaseUrl / abUpdateUrl 改成主机局域网 IP 即可。
    /// </summary>
    public static class ClientConfigLoader
    {
        const string FileName = "client_config.json";

        [Serializable]
        class Dto
        {
            public string mpBaseUrl;
            public string gameBaseUrl;
            public string abUpdateUrl;
            public string channel;
            public string region;
            public string platform;
            public string gameId;
            public string appId;
            public int clientVersionCode;
            public string resourceVersion;
        }

        public static ApiConfig Load()
        {
            var config = new ApiConfig();

            // 1) 可执行文件旁（或 Editor 工程根旁）覆盖
            var external = Path.Combine(Application.dataPath, "..", FileName);
            if (TryReadFile(external, out var jsonExt))
            {
                Apply(config, jsonExt);
                Debug.Log($"[ClientConfig] loaded external: {Path.GetFullPath(external)}");
                return config;
            }

            // 2) StreamingAssets
            var streaming = Path.Combine(Application.streamingAssetsPath, FileName);
            if (TryReadFile(streaming, out var jsonSa))
            {
                Apply(config, jsonSa);
                Debug.Log($"[ClientConfig] loaded StreamingAssets: {streaming}");
                return config;
            }

            Debug.LogWarning("[ClientConfig] no json found, using code defaults (localhost)");
            return config;
        }

        static bool TryReadFile(string path, out string json)
        {
            json = null;
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return false;
                json = File.ReadAllText(path);
                return !string.IsNullOrWhiteSpace(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ClientConfig] read failed {path}: {e.Message}");
                return false;
            }
        }

        static void Apply(ApiConfig config, string json)
        {
            var dto = JsonUtility.FromJson<Dto>(json);
            if (dto == null) return;

            if (!string.IsNullOrEmpty(dto.mpBaseUrl)) config.MpBaseUrl = dto.mpBaseUrl.TrimEnd('/');
            if (!string.IsNullOrEmpty(dto.gameBaseUrl)) config.GameBaseUrl = dto.gameBaseUrl.TrimEnd('/');
            if (!string.IsNullOrEmpty(dto.abUpdateUrl)) config.AbUpdateUrl = EnsureSlash(dto.abUpdateUrl);
            if (!string.IsNullOrEmpty(dto.channel)) config.Channel = dto.channel;
            if (!string.IsNullOrEmpty(dto.region)) config.Region = dto.region;
            if (!string.IsNullOrEmpty(dto.platform)) config.Platform = dto.platform;
            if (!string.IsNullOrEmpty(dto.gameId)) config.GameId = dto.gameId;
            if (!string.IsNullOrEmpty(dto.appId)) config.AppId = dto.appId;
            if (dto.clientVersionCode > 0) config.ClientVersionCode = dto.clientVersionCode;
            if (!string.IsNullOrEmpty(dto.resourceVersion)) config.ResourceVersion = dto.resourceVersion;
        }

        static string EnsureSlash(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            return url.EndsWith("/") ? url : url + "/";
        }
    }
}
