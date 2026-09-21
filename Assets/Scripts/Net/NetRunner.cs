using UnityEngine;
using VContainer;

namespace GameAct.Net
{
    /// <summary>
    /// 网络 Pump：必须早于 GameplayRunner.Update。
    /// Role==None（单机 / 已 Disconnect）时不 Poll，避免空转与残留会话副作用。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class NetRunner : MonoBehaviour
    {
        INetSession _session;

        [Inject]
        public void Construct(INetSession session)
        {
            _session = session;
        }

        public void Bind(INetSession session)
        {
            _session = session;
        }

        void Update()
        {
            if (_session == null || _session.Role == NetRole.None)
                return;
            _session.Poll();
        }

        void OnApplicationQuit()
        {
            _session?.Disconnect();
        }
    }
}
