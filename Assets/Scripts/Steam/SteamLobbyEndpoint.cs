using Steamworks;
using UnityEngine;

namespace GameAct.Steam
{
    /// <summary>
    /// 往当前 Steam Lobby 写入/读取 LES Host 端点。
    /// 支持双传输：SteamP2P（优先）与 UDP（局域网回退）。
    /// </summary>
    public static class SteamLobbyEndpoint
    {
        public const string KeyHost = "les_host";
        public const string KeyPort = "les_port";
        /// <summary>"steam" | "udp"</summary>
        public const string KeyTransport = "les_transport";
        /// <summary>Host SteamID64（SteamP2P 用）</summary>
        public const string KeySteamId = "les_steamid";
        /// <summary>Steam virtual port（默认 0）</summary>
        public const string KeyVPort = "les_vport";

        public const string TransportSteam = "steam";
        public const string TransportUdp = "udp";

        /// <summary>写入 UDP 端点（局域网 / 开发）。</summary>
        public static void Write(ulong lobbyId, string ip, int port)
        {
            if (lobbyId == 0 || string.IsNullOrEmpty(ip)) return;
            try
            {
                var id = new CSteamID(lobbyId);
                SteamMatchmaking.SetLobbyData(id, KeyHost, ip);
                SteamMatchmaking.SetLobbyData(id, KeyPort, port.ToString());
                SteamMatchmaking.SetLobbyData(id, KeyTransport, TransportUdp);
                Debug.Log($"[Steam] Lobby LES endpoint UDP {ip}:{port}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Steam] SetLobbyData: " + e.Message);
            }
        }

        /// <summary>写入 SteamP2P 端点（正式联机）。可同时保留 UDP 回退地址。</summary>
        public static void WriteSteamP2P(ulong lobbyId, ulong hostSteamId, int virtualPort = 0, string fallbackIp = null, int fallbackPort = 9050)
        {
            if (lobbyId == 0 || hostSteamId == 0) return;
            try
            {
                var id = new CSteamID(lobbyId);
                SteamMatchmaking.SetLobbyData(id, KeySteamId, hostSteamId.ToString());
                SteamMatchmaking.SetLobbyData(id, KeyVPort, virtualPort.ToString());
                SteamMatchmaking.SetLobbyData(id, KeyTransport, TransportSteam);

                if (!string.IsNullOrEmpty(fallbackIp))
                {
                    SteamMatchmaking.SetLobbyData(id, KeyHost, fallbackIp);
                    SteamMatchmaking.SetLobbyData(id, KeyPort, fallbackPort.ToString());
                }

                Debug.Log($"[Steam] Lobby LES endpoint SteamP2P steamId={hostSteamId} vport={virtualPort}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Steam] SetLobbyData SteamP2P: " + e.Message);
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

        public static string ReadTransport(ulong lobbyId)
        {
            if (lobbyId == 0) return TransportUdp;
            try
            {
                var t = SteamMatchmaking.GetLobbyData(new CSteamID(lobbyId), KeyTransport);
                if (t == TransportSteam) return TransportSteam;
            }
            catch { /* ignore */ }
            return TransportUdp;
        }

        public static ulong ReadSteamId(ulong lobbyId)
        {
            if (lobbyId == 0) return 0;
            try
            {
                var s = SteamMatchmaking.GetLobbyData(new CSteamID(lobbyId), KeySteamId);
                if (ulong.TryParse(s, out var id)) return id;
            }
            catch { /* ignore */ }
            return 0;
        }

        public static int ReadVirtualPort(ulong lobbyId, int fallback = 0)
        {
            if (lobbyId == 0) return fallback;
            try
            {
                var s = SteamMatchmaking.GetLobbyData(new CSteamID(lobbyId), KeyVPort);
                if (int.TryParse(s, out var p) && p >= 0) return p;
            }
            catch { /* ignore */ }
            return fallback;
        }

        /// <summary>是否应使用 SteamP2P（Lobby 标记为 steam 且有有效 SteamID）。</summary>
        public static bool PreferSteamP2P(ulong lobbyId)
        {
            return ReadTransport(lobbyId) == TransportSteam && ReadSteamId(lobbyId) != 0;
        }
    }
}
