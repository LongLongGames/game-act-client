using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using GameAct.AppFlow;
using GameAct.UI;
using GameAct.Steam;
using GameAct.Net;
using GameAct.Net.LiteNet;
using GameAct.Lobby;

namespace GameAct.Bootstrap
{
    /// <summary>
    /// 场景入口：创建 Canvas + 运行时 Login/Home UI，启动 AppFlow。
    /// 挂到 Demo 场景任意物体（与 GameLifetimeScope 同物体或子物体均可）。
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Inject] IAppFlow _appFlow;

        void Start()
        {
            BootstrapAsync().Forget();
        }

        async UniTaskVoid BootstrapAsync()
        {
            await UniTask.Yield();

            if (_appFlow == null)
            {
                ManualBootstrap();
                return;
            }

            var canvas = EnsureCanvas();
            var login = canvas.gameObject.AddComponent<RuntimeLoginView>();
            login.Build(canvas.transform);
            var home = canvas.gameObject.AddComponent<RuntimeHomeView>();
            home.Build(canvas.transform);

            if (_appFlow is AppFlowController)
            {
                ManualBootstrapWithViews(login, home);
                return;
            }

            await _appFlow.StartAsync();
        }

        void ManualBootstrap()
        {
            var canvas = EnsureCanvas();
            var login = canvas.gameObject.AddComponent<RuntimeLoginView>();
            login.Build(canvas.transform);
            var home = canvas.gameObject.AddComponent<RuntimeHomeView>();
            home.Build(canvas.transform);
            ManualBootstrapWithViews(login, home);
        }

        void ManualBootstrapWithViews(ILoginView login, IHomeView home)
        {
            var config = new Network.ApiConfig();
            var http = new Network.HttpClientService();
            var tokenStore = new Services.TokenStore();
            var auth = new Auth.AuthService(http, config, tokenStore);
            var version = new Services.VersionService(http, config);
            var player = new Services.PlayerService(http, config);

            var steam = new SteamService();
            var net = new LiteNetSession();
            var lobby = new LobbyService(http, config);

            var runners = new GameObject("P1_Runners");
            DontDestroyOnLoad(runners);
            runners.AddComponent<SteamRunner>().Bind(steam);
            runners.AddComponent<NetRunner>().Bind(net);

            var flow = new AppFlowController(version, auth, player, http, login, home, steam, net, config, lobby);
            flow.StartAsync().Forget();
        }

        static Canvas EnsureCanvas()
        {
            var existing = FindFirstObjectByType<Canvas>();
            if (existing != null) return existing;

            var go = new GameObject("UICanvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();

            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            return canvas;
        }
    }
}
