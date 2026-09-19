using UnityEngine;
using VContainer;
using VContainer.Unity;
using GameAct.Network;
using GameAct.Auth;
using GameAct.Services;
using GameAct.AppFlow;
using GameAct.UI;
using GameAct.Steam;
using GameAct.Net;
using GameAct.Net.LiteNet;

namespace GameAct.DI
{
    /// <summary>
    /// P1 根 LifetimeScope。挂在 Bootstrap 场景物体上。
    /// View 仍由 Runtime 创建后手动拼装（见 GameBootstrap）。
    /// </summary>
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] ApiConfigOverride configOverride;

        protected override void Configure(IContainerBuilder builder)
        {
            var config = new ApiConfig();
            if (configOverride != null)
            {
                if (!string.IsNullOrEmpty(configOverride.mpBaseUrl))
                    config.MpBaseUrl = configOverride.mpBaseUrl;
                if (!string.IsNullOrEmpty(configOverride.gameBaseUrl))
                    config.GameBaseUrl = configOverride.gameBaseUrl;
                if (!string.IsNullOrEmpty(configOverride.gameId))
                    config.GameId = configOverride.gameId;
                if (!string.IsNullOrEmpty(configOverride.appId))
                    config.AppId = configOverride.appId;
                if (configOverride.clientVersionCode > 0)
                    config.ClientVersionCode = configOverride.clientVersionCode;
                if (!string.IsNullOrEmpty(configOverride.resourceVersion))
                    config.ResourceVersion = configOverride.resourceVersion;
            }

            builder.RegisterInstance(config);

            builder.Register<IHttpClient, HttpClientService>(Lifetime.Singleton);
            builder.Register<ITokenStore, TokenStore>(Lifetime.Singleton);
            builder.Register<IAuthService, AuthService>(Lifetime.Singleton);
            builder.Register<IVersionService, VersionService>(Lifetime.Singleton);
            builder.Register<IPlayerService, PlayerService>(Lifetime.Singleton);

            builder.Register<ISteamService, SteamService>(Lifetime.Singleton);
            builder.Register<INetSession, LiteNetSession>(Lifetime.Singleton);

            builder.Register<IAppFlow, AppFlowController>(Lifetime.Singleton);
        }
    }

    [System.Serializable]
    public class ApiConfigOverride
    {
        public string mpBaseUrl = "http://localhost:11080";
        public string gameBaseUrl = "http://localhost:13280";
        public string gameId = "act";
        public string appId = "test_app";
        public int clientVersionCode = 1;
        public string resourceVersion = "0.0.1";
    }
}
