using UnityEngine;

namespace GameAct.Gameplay.Simulation
{
    /// <summary>
    /// 实体在某一时刻的位姿（模拟权威或插值结果）。
    /// </summary>
    public struct EntityPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public bool Grounded;

        public static EntityPose Identity => new EntityPose
        {
            Position = Vector3.zero,
            Rotation = Quaternion.identity,
            Velocity = Vector3.zero,
            Grounded = true
        };
    }
}
