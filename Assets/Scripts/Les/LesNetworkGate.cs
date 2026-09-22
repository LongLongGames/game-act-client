using System;
using Cysharp.Threading.Tasks;
using GameAct.AppFlow;
using GameAct.Net;
using GameAct.Steam;
using GameAct.UI;
using UnityEngine;

namespace GameAct.Les
{
    /// <summary>
    /// AppFlow 进局网络门面：
    /// - Host：优先 SteamP2P（Steam 可用时），否则 UDP；Lobby 写入对应端点。
    /// - Client：读 Lobby transport，SteamP2P 用 Host SteamID，UDP 用 IP:Port。
    /// </summary>
    public static class LesNetworkGate
    {
        public const int Port = LesNetworkHub.DefaultPort;

        /// <summary>
        /// 全局偏好：true=Steam 可用时强制 SteamP2P；false=仅 UDP。
        /// 可在设置或启动参数里改。
        /// </summary>
        public static bool PreferSteamP2PWhenAvailable { get; set; } = true;

        public static async UniTask EnsureAsync(
            INetSession net,
            ISteamService steam,
            SessionMode mode,
            ILoadingView loading)
        {
            switch (mode)
            {
                case SessionMode.Solo:
                    net?.Disconnect();
                    return;

                case SessionMode.Host:
                {
                    if (net == null)
                        throw new InvalidOperationException("网络会话未注册（需要 LesNetworkHub）");

                    bool useSteam = PreferSteamP2PWhenAvailable
                                    && steam != null
                                    && steam.IsInitialized
                                    && steam.SteamId != 0;

                    if (!(net.IsConnected && net.Role == NetRole.Host))
                    {
                        loading?.SetStatus(useSteam
                            ? "正在启动 LES Host (SteamP2P)…"
                            : "正在启动 LES Host (UDP)…");

                        var kind = useSteam ? NetTransportKind.SteamP2P : NetTransportKind.Udp;
                        var ok = await net.StartHostAsync(Port, kind);
                        if (!ok)
                            throw new InvalidOperationException("LES Host 启动失败");
                    }

                    // 写 Lobby 端点
                    if (steam != null && steam.CurrentLobbyId != 0)
                    {
                        if (useSteam)
                        {
                            SteamLobbyEndpoint.WriteSteamP2P(
                                steam.CurrentLobbyId,
                                steam.SteamId,
                                LesNetworkHub.DefaultSteamVirtualPort,
                                LesNetworkHub.GetLanIPv4(),
                                Port);
                        }
                        else
                        {
                            SteamLobbyEndpoint.Write(
                                steam.CurrentLobbyId,
                                LesNetworkHub.GetLanIPv4(),
                                Port);
                        }
                    }
                    return;
                }

                case SessionMode.Client:
                {
                    if (net == null)
                        throw new InvalidOperationException("网络会话未注册（需要 LesNetworkHub）");
                    if (net.IsConnected && net.Role == NetRole.Client)
                        return;

                    ulong lobbyId = steam?.CurrentLobbyId ?? 0;
                    bool useSteam = PreferSteamP2PWhenAvailable
                                    && SteamLobbyEndpoint.PreferSteamP2P(lobbyId);

                    if (net is LesNetworkHub hub && steam != null && !string.IsNullOrEmpty(steam.PersonaName))
                        hub.SetUserName(steam.PersonaName);

                    if (useSteam)
                    {
                        ulong hostSteamId = SteamLobbyEndpoint.ReadSteamId(lobbyId);
                        int vport = SteamLobbyEndpoint.ReadVirtualPort(lobbyId, LesNetworkHub.DefaultSteamVirtualPort);
                        loading?.SetStatus($"正在连接 Host SteamP2P {hostSteamId}…");
                        var ok = await net.ConnectAsync(hostSteamId.ToString(), vport, NetTransportKind.SteamP2P);
                        if (!ok)
                            throw new InvalidOperationException($"SteamP2P 连接失败 steamId={hostSteamId}");
                    }
                    else
                    {
                        var ip = SteamLobbyEndpoint.ReadHost(lobbyId);
                        if (string.IsNullOrWhiteSpace(ip))
                            ip = "127.0.0.1";
                        int port = SteamLobbyEndpoint.ReadPort(lobbyId, Port);

                        loading?.SetStatus($"正在连接 Host UDP {ip}:{port}…");
                        var ok = await net.ConnectAsync(ip, port, NetTransportKind.Udp);
                        if (!ok)
                            throw new InvalidOperationException($"连接 Host 失败 {ip}:{port}");
                    }
                    return;
                }

                default:
                    throw new InvalidOperationException("未知 SessionMode: " + mode);
            }
        }
    }
}
