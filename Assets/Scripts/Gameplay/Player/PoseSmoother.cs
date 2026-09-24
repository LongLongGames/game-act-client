using UnityEngine;

namespace GameAct.Gameplay.Player
{
    /// <summary>
    /// 权威端（Solo / Host）的渲染插值。
    ///
    /// 背景：LES 只在 Client 端提供 SyncVar.InterpolatedValue；Solo / Host 的 ActPlayer 位置
    /// 只在 30Hz 逻辑 tick 里改一次，直接拿来显示就是阶梯——角色一抖一抖，相机再怎么平滑也救不了。
    ///
    /// 做法：每帧检测逻辑位姿是否出现了新值（= 跑了新 tick），出现时从「当前已渲染的位姿」
    /// 出发，用一个 tick 的时长追向新值。始终从已渲染位置出发，所以永远没有跳变；
    /// 代价是显示比逻辑晚约一个 tick（33ms），仅表现层，不影响逻辑。
    /// </summary>
    public sealed class PoseSmoother
    {
        const float TeleportDistance = 5f;

        readonly float _tickInterval;

        Vector3 _from, _to, _render;
        float _fromYaw, _toYaw, _renderYaw;
        float _t;
        float _interval;
        bool _inited;

        public Vector3 Position => _render;
        public float Yaw => _renderYaw;

        public PoseSmoother(float tickRate)
        {
            _tickInterval = 1f / Mathf.Max(1f, tickRate);
            _interval = _tickInterval;
        }

        public void Reset(Vector3 pos, float yaw)
        {
            _from = _to = _render = pos;
            _fromYaw = _toYaw = _renderYaw = yaw;
            _t = _interval;
            _inited = true;
        }

        /// <summary>每渲染帧调用一次，传入当前逻辑位姿。</summary>
        public void Advance(Vector3 logicPos, float logicYaw, float dt)
        {
            if (!_inited)
            {
                Reset(logicPos, logicYaw);
                return;
            }

            bool changed = (logicPos - _to).sqrMagnitude > 1e-8f
                           || Mathf.Abs(Mathf.DeltaAngle(logicYaw, _toYaw)) > 1e-3f;

            if (changed)
            {
                if ((logicPos - _render).sqrMagnitude > TeleportDistance * TeleportDistance)
                {
                    Reset(logicPos, logicYaw);
                    return;
                }

                // 帧率低于 tick 率时（一帧跑多个 tick），追赶时长跟着拉长，避免"到位后停住等下一帧"。
                _interval = Mathf.Max(_tickInterval, Time.smoothDeltaTime);
                _from = _render;
                _fromYaw = _renderYaw;
                _to = logicPos;
                _toYaw = logicYaw;
                _t = 0f;
            }

            _t += dt;
            float a = Mathf.Clamp01(_t / _interval);
            _render = Vector3.Lerp(_from, _to, a);
            _renderYaw = Mathf.LerpAngle(_fromYaw, _toYaw, a);
        }
    }
}
