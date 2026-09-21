using UnityEngine;

namespace GameAct.Gameplay.Camera
{
    /// <summary>
    /// 逻辑相机：纯数据，根据角色位姿算出「理想」相机位置与朝向。
    /// 不依赖 Unity Camera 组件，不参与平滑/碰撞，可在预测回滚路径中使用。
    /// </summary>
    public class LogicCamera
    {
        public float Distance = 6f;
        public float Height = 2.5f;
        public float LookAtHeight = 1.5f;

        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }
        public Vector3 LookAtPoint { get; private set; }
        public Vector3 Forward { get; private set; }

        /// <summary>
        /// 根据角色世界位置 + Yaw（度）更新理想相机。
        /// 与 UnityExample ClientPlayerView 的 offset 计算一致。
        /// </summary>
        public void UpdateFromPlayer(Vector3 playerPos, float yawDegrees)
        {
            float yaw = yawDegrees * Mathf.Deg2Rad;

            // 相机在角色身后上方
            Vector3 offset = new Vector3(
                -Mathf.Sin(yaw) * Distance,
                Height,
                -Mathf.Cos(yaw) * Distance
            );

            Position = playerPos + offset;
            LookAtPoint = playerPos + Vector3.up * LookAtHeight;

            Forward = (LookAtPoint - Position).normalized;
            if (Forward.sqrMagnitude < 1e-6f)
                Forward = Vector3.forward;

            Rotation = Quaternion.LookRotation(Forward, Vector3.up);
        }

        /// <summary>复制参数，方便多实例共享配置。</summary>
        public void CopySettingsFrom(LogicCamera other)
        {
            Distance = other.Distance;
            Height = other.Height;
            LookAtHeight = other.LookAtHeight;
        }
    }
}
