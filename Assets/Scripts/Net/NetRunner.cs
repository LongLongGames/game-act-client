using UnityEngine;
using VContainer;
using GameAct.Les;
using GameAct.Les.Transport;

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
        bool _quitting;

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
            if (_quitting) return;
            if (_session == null || _session.Role == NetRole.None)
                return;
            _session.Poll();
        }

        void OnApplicationQuit()
        {
            _quitting = true;
            SafeDisconnect(isQuit: true);
        }

        void OnDestroy()
        {
            if (!Application.isPlaying)
            {
                // Domain unload：只软清，不碰可能已坏的 Steam Native
                _quitting = true;
                SafeDisconnect(isQuit: true);
                return;
            }
            if (_quitting) return;
            SafeDisconnect(isQuit: false);
        }

        void SafeDisconnect(bool isQuit)
        {
            if (_session == null) return;
            try
            {
                if (isQuit && _session is LesNetworkHub hub)
                {
                    // 退出时走 Soft：不 CloseConnection / 不 DestroyPollGroup
                    hub.DisconnectSoft();
                }
                else
                {
                    _session.Disconnect();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[NetRunner] Disconnect: " + e.Message);
            }
        }
    }
}
