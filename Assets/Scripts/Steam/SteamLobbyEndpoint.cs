using Steamworks;
using UnityEngine;

namespace GameAct.Steam
{
    /// <summary>往当前 Steam Lobby 写入/读取 LES Host 地址，不改 ISteamService 签名。</summary>
    public static class SteamLobbyEndpoint
    {
        public const string KeyHost = "les_host";
        public const string KeyPort = "les_port";

        public static void Write(ulong lobbyId, string ip, int port)
        {
            if (lobbyId == 0 || string.IsNullOrEmpty(ip)) return;
            try
            {
                var id = new CSteamID(lobbyId);
                SteamMatchmaking.SetLobbyData(id, KeyHost, ip);
                SteamMatchmaking.SetLobbyData(id, KeyPort, port.ToString());
                Debug.Log($"[Steam] Lobby LES endpoint {ip}:{port}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Steam] SetLobbyData: " + e.Message);
            }
        }

        public static string ReadHost(ulong lobbyId)
        {
            if (lobbyId == 0) return null;
            try
            {
                return SteamMatchmaking.GetLobbyData(new CSteamID(lobbyId), KeyHost);
            }
            catch
            {
                return null;
            }
        }

        public static int ReadPort(ulong lobbyId, int fallback)
        {
            if (lobbyId == 0) return fallback;
            try
            {
                var s = SteamMatchmaking.GetLobbyData(new CSteamID(lobbyId), KeyPort);
                if (int.TryParse(s, out var p) && p > 0) return p;
            }
            catch { /* ignore */ }
            return fallback;
        }
    }
}
