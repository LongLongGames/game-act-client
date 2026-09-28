using LiteEntitySystem;
using UnityEngine;
using GameAct.Spatial;

namespace GameAct.Les.Shared
{
    public class ActMonster : PawnLogic
    {
        const float MoveSpeed = 2.8f;
        /// <summary>离地后下落加速度（m/s²）</summary>
        const float Gravity = 20f;
        /// <summary>最大下落速度</summary>
        const float MaxFallSpeed = 40f;

        [SyncVarFlags(SyncFlags.Interpolated | SyncFlags.LagCompensated)]
        SyncVar<Vector3> _position;

        [SyncVarFlags(SyncFlags.Interpolated)]
        SyncVar<float> _yaw;

        Vector3 _moveDir;
        bool _dead;

        float _velY;
        bool _grounded = true;

        public Vector3 Position => _position.Value;
        public float Yaw => _yaw.Value;
        public float SpeedXZ => _dead ? 0f : _moveDir.magnitude * MoveSpeed;
        public bool IsDead => _dead;
        public bool IsGrounded => _grounded;

        public ActMonster(EntityParams entityParams) : base(entityParams) { }

        public void Spawn(Vector3 position)
        {
            _position.Value = position;
            _yaw.Value = Random.Range(0f, 360f);
            _moveDir = Vector3.zero;
            _dead = false;
            _velY = 0f;
            _grounded = true;
            _position.Value = WorldMotor.SnapToGround(_position.Value);
        }

        public void MarkDead()
        {
            _dead = true;
            _moveDir = Vector3.zero;
            _velY = 0f;
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
                ref _velY, ref _grounded,
                WorldMotor.DefaultRadius * 0.9f, WorldMotor.DefaultHeight * 0.85f);
            _position.Value = next;
        }

        protected override void Update()
        {
            base.Update();
            if (_dead) return;

            float dt = EntityManager.DeltaTimeF;

            // 离地：累加重力速度
            if (!_grounded)
            {
                _velY -= Gravity * dt;
                if (_velY < -MaxFallSpeed)
                    _velY = -MaxFallSpeed;
            }

            Vector3 delta = Vector3.zero;
            if (_moveDir.sqrMagnitude > 0.0001f)
                delta = new Vector3(_moveDir.x, 0f, _moveDir.z) * (MoveSpeed * dt);

            // 竖直位移只用重力积分，不靠 Snap 大段拉下
            Vector3 pos = _position.Value;
            if (!_grounded && Mathf.Abs(_velY) > 1e-6f)
                pos.y += _velY * dt;

            // 每帧都要走（停步也会下落/贴地）
            _position.Value = WorldMotor.MoveHorizontalAndSnap(
                pos, delta,
                ref _velY, ref _grounded,
                WorldMotor.DefaultRadius * 0.9f, WorldMotor.DefaultHeight * 0.85f);
        }
    }
}
