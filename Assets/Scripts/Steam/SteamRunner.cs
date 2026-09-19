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
            _steam?.RunCallbacks();
        }

        void OnApplicationQuit()
        {
            _steam?.Shutdown();
        }
    }
}
