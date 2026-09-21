using UnityEngine;
using VContainer;
using VContainer.Unity;
using GameAct.Network;
using GameAct.Auth;
using GameAct.Services;
using GameAct.AppFlow;
using GameAct.Steam;
using GameAct.Net;
using GameAct.Net.LiteNet;
using GameAct.Lobby;

namespace GameAct.DI
{
    /// <summary>
    /// 根 LifetimeScope。
    /// INetSession 注册为单例但不自动 Start；Solo 保持 Role=None。
    /// </summary>
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var config = ClientConfigLoader.Load();
            builder.RegisterInstance(config);

            builder.Register<IHttpClient, HttpClientService>(Lifetime.Singleton);
            builder.Register<ITokenStore, TokenStore>(Lifetime.Singleton);
            builder.Register<IAuthService, AuthService>(Lifetime.Singleton);
            builder.Register<IVersionService, VersionService>(Lifetime.Singleton);
            builder.Register<IPlayerService, PlayerService>(Lifetime.Singleton);

            builder.Register<ISteamService, SteamService>(Lifetime.Singleton);
            builder.Register<INetSession, LiteNetSession>(Lifetime.Singleton);
            builder.Register<ILobbyService, LobbyService>(Lifetime.Singleton);

            builder.Register<IAppFlow, AppFlowController>(Lifetime.Singleton);
        }
    }
}
