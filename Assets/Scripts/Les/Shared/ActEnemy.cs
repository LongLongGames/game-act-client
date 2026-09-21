using LiteEntitySystem;
using UnityEngine;

namespace GameAct.Les.Shared
{
    /// <summary>
    /// 可被 AiControllerLogic 控制的敌人 Pawn。
    /// 不做玩家回滚；仅权威端积分（Solo/Host 的 ServerEntityManager）。
    /// </summary>
    public class ActEnemy : PawnLogic
    {
        const float MoveSpeed = 2.8f;

        [SyncVarFlags(SyncFlags.Interpolated | SyncFlags.LagCompensated)]
        SyncVar<Vector3> _position;

        [SyncVarFlags(SyncFlags.Interpolated)]
        SyncVar<float> _yaw;

        Vector3 _moveDir;
        bool _hasGround;

        public Vector3 Position => _position.Value;
        public float Yaw => _yaw.Value;
        public Vector3 RenderPosition => _position.InterpolatedValue;
        public float RenderYaw => _yaw.InterpolatedValue;

        public ActEnemy(EntityParams entityParams) : base(entityParams) { }

        public void Spawn(Vector3 position)
        {
            _position.Value = position;
            _yaw.Value = Random.Range(0f, 360f);
            _moveDir = Vector3.zero;
            _hasGround = true;
        }

        /// <summary>由 EnemyBotController 在 BeforeControlledUpdate 写入。</summary>
        public void SetInput(Vector3 worldMoveDir, float yawDegrees)
        {
            _moveDir = worldMoveDir;
            if (_moveDir.sqrMagnitude > 1f)
                _moveDir.Normalize();
            _yaw.Value = yawDegrees;
        }

        // 跨程序集覆盖 LES 的 protected internal → 只能用 protected
        protected override void Update()
        {
            // Controller 先写入输入
            base.Update();

            float dt = EntityManager.DeltaTimeF;
            if (_moveDir.sqrMagnitude > 0.0001f)
            {
                var delta = _moveDir * (MoveSpeed * dt);
                var next = _position.Value + new Vector3(delta.x, 0f, delta.z);
                next = SnapToGround(next);
                _position.Value = next;
            }
            else if (!_hasGround)
            {
                _position.Value = SnapToGround(_position.Value);
            }
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
