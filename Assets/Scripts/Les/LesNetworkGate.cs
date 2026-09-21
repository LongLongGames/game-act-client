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
    /// AppFlow 进局网络门面：Host 起 LES+UDP，Client 从 Steam Lobby 读地址连接。
    /// </summary>
    public static class LesNetworkGate
    {
        public const int Port = LesNetworkHub.DefaultPort;

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
                    if (!(net.IsConnected && net.Role == NetRole.Host))
                    {
                        loading?.SetStatus("正在启动 LES Host…");
                        var ok = await net.StartHostAsync(Port);
                        if (!ok)
                            throw new InvalidOperationException("LES Host 启动失败");
                    }

                    if (steam != null)
                        SteamLobbyEndpoint.Write(steam.CurrentLobbyId, LesNetworkHub.GetLanIPv4(), Port);
                    return;
                }

                case SessionMode.Client:
                {
                    if (net == null)
                        throw new InvalidOperationException("网络会话未注册（需要 LesNetworkHub）");
                    if (net.IsConnected && net.Role == NetRole.Client)
                        return;

                    ulong lobbyId = steam?.CurrentLobbyId ?? 0;
                    var ip = SteamLobbyEndpoint.ReadHost(lobbyId);
                    if (string.IsNullOrWhiteSpace(ip))
                        ip = "127.0.0.1";
                    int port = SteamLobbyEndpoint.ReadPort(lobbyId, Port);

                    loading?.SetStatus($"正在连接 Host {ip}:{port}…");
                    if (net is LesNetworkHub hub && steam != null && !string.IsNullOrEmpty(steam.PersonaName))
                        hub.SetUserName(steam.PersonaName);

                    var ok = await net.ConnectAsync(ip, port);
                    if (!ok)
                        throw new InvalidOperationException($"连接 Host 失败 {ip}:{port}");
                    return;
                }

                default:
                    throw new InvalidOperationException("未知 SessionMode: " + mode);
            }
        }
    }
}
