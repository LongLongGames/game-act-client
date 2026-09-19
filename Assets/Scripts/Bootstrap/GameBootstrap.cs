using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using GameAct.AppFlow;
using GameAct.UI;
using GameAct.Steam;
using GameAct.Net;
using GameAct.Net.LiteNet;
using GameAct.Network;

namespace GameAct.Bootstrap
{
    /// <summary>
    /// 场景入口：创建 Canvas + 各独立 UI，启动 AppFlow。
    /// 配置统一走 ClientConfigLoader（StreamingAssets/client_config.json）。
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
            ManualBootstrap();
        }

        void ManualBootstrap()
        {
            var canvas = EnsureCanvas();
            var root = canvas.transform;

            var login = canvas.gameObject.AddComponent<RuntimeLoginView>();
            login.Build(root);

            var mainMenu = canvas.gameObject.AddComponent<RuntimeMainMenuView>();
            mainMenu.Build(root);

            var lobby = canvas.gameObject.AddComponent<RuntimeLobbyView>();
            lobby.Build(root);

            var room = canvas.gameObject.AddComponent<RuntimeRoomView>();
            room.Build(root);

            var settings = canvas.gameObject.AddComponent<RuntimeSettingsView>();
            settings.Build(root);

            var loading = canvas.gameObject.AddComponent<RuntimeLoadingView>();
            loading.Build(root);

            var config = ClientConfigLoader.Load();
            Debug.Log($"[Bootstrap] MP={config.MpBaseUrl} Game={config.GameBaseUrl} AB={config.AbUpdateUrl}");

            var http = new HttpClientService();
            var tokenStore = new Services.TokenStore();
            var auth = new Auth.AuthService(http, config, tokenStore);
            var version = new Services.VersionService(http, config);
            var player = new Services.PlayerService(http, config);

            var steam = new SteamService();
            var net = new LiteNetSession();

            var runners = new GameObject("P1_Runners");
            DontDestroyOnLoad(runners);
            runners.AddComponent<SteamRunner>().Bind(steam);
            runners.AddComponent<NetRunner>().Bind(net);

            var flow = new AppFlowController(
                version, auth, player, http,
                login, mainMenu, lobby, room, settings,
                loading, steam, net, config);
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
