using LiteEntitySystem;
using UnityEngine;

namespace GameAct.Les.Shared
{
    public class ActMonster : PawnLogic
    {
        const float MoveSpeed = 2.8f;

        [SyncVarFlags(SyncFlags.Interpolated | SyncFlags.LagCompensated)]
        SyncVar<Vector3> _position;

        [SyncVarFlags(SyncFlags.Interpolated)]
        SyncVar<float> _yaw;

        Vector3 _moveDir;

        public Vector3 Position => _position.Value;
        public float Yaw => _yaw.Value;
        /// <summary>当前水平速度（用于 View Idle↔Move）。</summary>
        public float SpeedXZ => _moveDir.magnitude * MoveSpeed;

        public ActMonster(EntityParams entityParams) : base(entityParams) { }

        public void Spawn(Vector3 position)
        {
            _position.Value = position;
            _yaw.Value = Random.Range(0f, 360f);
            _moveDir = Vector3.zero;
        }

        public void SetInput(Vector3 worldMoveDir, float yawDegrees)
        {
            _moveDir = worldMoveDir;
            if (_moveDir.sqrMagnitude > 1f)
                _moveDir.Normalize();
            _yaw.Value = yawDegrees;
        }

        // 跨程序集覆盖 protected internal → protected
        protected override void Update()
        {
            base.Update();
            float dt = EntityManager.DeltaTimeF;
            if (_moveDir.sqrMagnitude <= 0.0001f) return;
            var delta = _moveDir * (MoveSpeed * dt);
            var next = _position.Value + new Vector3(delta.x, 0f, delta.z);
            _position.Value = SnapToGround(next);
        }

        static Vector3 SnapToGround(Vector3 pos)
        {
            var origin = pos + Vector3.up * 3f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.05f;
            return new Vector3(pos.x, Mathf.Max(pos.y, 0.05f), pos.z);
        }
    }
}
