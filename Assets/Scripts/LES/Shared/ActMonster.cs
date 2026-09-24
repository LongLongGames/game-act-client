using LiteEntitySystem;
using UnityEngine;
using GameAct.Spatial;

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
        bool _dead;

        public Vector3 Position => _position.Value;
        public float Yaw => _yaw.Value;
        public float SpeedXZ => _dead ? 0f : _moveDir.magnitude * MoveSpeed;
        public bool IsDead => _dead;

        public ActMonster(EntityParams entityParams) : base(entityParams) { }

        public void Spawn(Vector3 position)
        {
            _position.Value = position;
            _yaw.Value = Random.Range(0f, 360f);
            _moveDir = Vector3.zero;
            _dead = false;
        }

        public void MarkDead()
        {
            _dead = true;
            _moveDir = Vector3.zero;
        }

        public void SetInput(Vector3 worldMoveDir, float yawDegrees)
        {
            if (_dead)
            {
                _moveDir = Vector3.zero;
                return;
            }
            _moveDir = worldMoveDir;
            if (_moveDir.sqrMagnitude > 1f)
                _moveDir.Normalize();
            _yaw.Value = yawDegrees;
        }

        /// <summary>
        /// 权威击退：水平 Slide + 贴地，避免推进墙体。
        /// </summary>
        public void ApplyKnockback(Vector3 worldDir, float distance)
        {
            if (_dead || distance <= 0.001f) return;

            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 1e-8f) return;
            worldDir.Normalize();

            var next = WorldMotor.MoveHorizontalAndSnap(
                _position.Value, worldDir * distance,
                WorldMotor.DefaultRadius * 0.9f, WorldMotor.DefaultHeight * 0.85f);
            _position.Value = next;
        }

        protected override void Update()
        {
            base.Update();
            if (_dead) return;
            float dt = EntityManager.DeltaTimeF;
            if (_moveDir.sqrMagnitude <= 0.0001f) return;
            var delta = new Vector3(_moveDir.x, 0f, _moveDir.z) * (MoveSpeed * dt);
            _position.Value = WorldMotor.MoveHorizontalAndSnap(
                _position.Value, delta,
                WorldMotor.DefaultRadius * 0.9f, WorldMotor.DefaultHeight * 0.85f);
        }

        /* ---- 旧仅 Snap 贴地（无水平挡墙）----
        static Vector3 SnapToGround(Vector3 pos)
        {
            var origin = pos + Vector3.up * 3f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.05f;
            return new Vector3(pos.x, Mathf.Max(pos.y, 0.05f), pos.z);
        }
        ---- */
    }
}
