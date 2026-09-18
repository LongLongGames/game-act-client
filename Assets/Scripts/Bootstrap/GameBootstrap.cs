using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using GameAct.AppFlow;
using GameAct.UI;

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
            // 等 VContainer 注入完成
            await UniTask.Yield();

            if (_appFlow == null)
            {
                // 若未挂 LifetimeScope，走手动组装（方便无 DI 场景快速验证）
                Debug.LogWarning("[Bootstrap] IAppFlow not injected, building manual graph");
                ManualBootstrap();
                return;
            }

            var canvas = EnsureCanvas();
            var login = canvas.gameObject.AddComponent<RuntimeLoginView>();
            login.Build(canvas.transform);
            var home = canvas.gameObject.AddComponent<RuntimeHomeView>();
            home.Build(canvas.transform);

            // 通过反射把 view 注入到已存在的 AppFlow（DI 已注册但 view 是运行时创建）
            // 更干净做法：把 view 也注册进 scope。这里为 P0 简单覆盖字段。
            if (_appFlow is AppFlowController flow)
            {
                // AppFlowController 已在构造时注入 view；若 DI 未提供 view 则需重建。
                // 当前 DI 未注册 view，改为手动创建完整图：
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
            var flow = new AppFlow.AppFlowController(version, auth, player, http, login, home);
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
