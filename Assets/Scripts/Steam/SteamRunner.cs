using UnityEngine;
using VContainer;

namespace GameAct.Steam
{
    /// <summary>
    /// 挂场景上，每帧跑 Steam 回调。由 Bootstrap 或 DI 创建。
    /// </summary>
    public class SteamRunner : MonoBehaviour
    {
        ISteamService _steam;
        bool _shuttingDown;

        [Inject]
        public void Construct(ISteamService steam)
        {
            _steam = steam;
        }

        public void Bind(ISteamService steam)
        {
            _steam = steam;
        }

        void Update()
        {
            if (_shuttingDown) return;
            _steam?.RunCallbacks();
        }

        void OnApplicationQuit()
        {
            // Editor Stop / 进程退出：SteamService.Shutdown 内部已对 Editor 跳过
            // SteamAPI.Shutdown，并对坏 pipe 做防护。
            SafeShutdown();
        }

        void OnDestroy()
        {
            // Domain reload / 销毁时再兜一次
            SafeShutdown();
        }

        void SafeShutdown()
        {
            if (_shuttingDown) return;
            _shuttingDown = true;
            try
            {
                _steam?.Shutdown();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SteamRunner] Shutdown: " + e.Message);
            }
        }
    }
}
