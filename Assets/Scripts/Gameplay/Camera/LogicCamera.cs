using UnityEngine;

namespace GameAct.Gameplay.Camera
{
    /// <summary>
    /// 逻辑相机：纯数据，由「焦点位置 + yaw + pitch」算出相机的位置与朝向。
    /// 不依赖 Unity Camera 组件，不参与平滑/碰撞。
    ///
    /// 约定与 ActPlayer 一致：yaw=0 朝 +Z，随 yaw 增大转向 +X；pitch 为正 = 向下看。
    /// 相机始终沿自己的 forward 看向 LookAtPoint，距离 Distance，角色恒在画面中心。
    /// </summary>
    public class LogicCamera
    {
        public float Distance = 6f;
        public float LookAtHeight = 1.5f;

        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; } = Quaternion.identity;
        public Vector3 LookAtPoint { get; private set; }
        public Vector3 Forward { get; private set; } = Vector3.forward;

        /// <summary>视线在水平面上的前方（单位向量），做瞄准 / 相对移动时用。</summary>
        public Vector3 PlanarForward { get; private set; } = Vector3.forward;

        /// <summary>视线在水平面上的右方（单位向量）。</summary>
        public Vector3 PlanarRight { get; private set; } = Vector3.right;

        public void UpdateFromLook(Vector3 focusPos, float yawDegrees, float pitchDegrees)
        {
            Rotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
            Forward = Rotation * Vector3.forward;

            float yawRad = yawDegrees * Mathf.Deg2Rad;
            PlanarForward = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
            PlanarRight = new Vector3(Mathf.Cos(yawRad), 0f, -Mathf.Sin(yawRad));

            LookAtPoint = focusPos + Vector3.up * LookAtHeight;
            Position = LookAtPoint - Forward * Distance;
        }

        public void CopySettingsFrom(LogicCamera other)
        {
            Distance = other.Distance;
            LookAtHeight = other.LookAtHeight;
        }
    }
}
