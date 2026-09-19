using UnityEngine;
using VContainer;

namespace GameAct.Net
{
    /// <summary>
    /// 每帧 Poll LiteNetLib 事件。
    /// </summary>
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
            _session?.Poll();
        }

        void OnApplicationQuit()
        {
            _session?.Disconnect();
        }
    }
}
