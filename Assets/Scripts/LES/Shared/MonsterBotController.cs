using LiteEntitySystem;
using UnityEngine;
using GameAct.Spatial;

namespace GameAct.Les.Shared
{
    /// <summary>
    /// 怪物基础 AI：
    /// - Idle 游荡
    /// - 仇恨范围进入 → Chase（优先 FlowField，无场则直线追）
    /// - 超出脱战范围 → 回 Idle
    /// - 近距离面向目标（为后续攻击预留）
    /// </summary>
    public class MonsterBotController : AiControllerLogic<ActMonster>
    {
        /// <summary>进入仇恨的半径（米）</summary>
        public static float AggroRange = 8f;
        /// <summary>脱战半径（米），应大于 AggroRange</summary>
        public static float LoseAggroRange = 16f;
        /// <summary>认为“贴身”可攻击的距离</summary>
        public static float MeleeRange = 1.6f;
        /// <summary>游荡转向间隔</summary>
        public static float IdleTurnMin = 0.7f;
        public static float IdleTurnMax = 2.2f;

        float _yaw;
        float _changeTimer;
        int _targetPlayerId = -1;
        Vector3 _lastKnownTargetPos;
        bool _hasAggro;

        public MonsterBotController(EntityParams entityParams) : base(entityParams)
        {
            _yaw = Random.Range(0f, 360f);
            _changeTimer = Random.Range(0.4f, 1.5f);
        }

        protected override void BeforeControlledUpdate()
        {
            var pawn = ControlledEntity;
            if (pawn == null) return;

            if (pawn.IsDead)
            {
                pawn.SetInput(Vector3.zero, pawn.Yaw);
                _hasAggro = false;
                _targetPlayerId = -1;
                return;
            }

            float dt = EntityManager.DeltaTimeF;
            Vector3 myPos = pawn.Position;

            // 维护 / 寻找目标
            ActPlayer target = ResolveTarget(myPos);
            if (target != null)
            {
                _hasAggro = true;
                _targetPlayerId = target.Id;
                _lastKnownTargetPos = target.Position;
                Chase(pawn, target.Position, dt);
            }
            else
            {
                _hasAggro = false;
                _targetPlayerId = -1;
                IdleWander(pawn, dt);
            }
        }

        ActPlayer ResolveTarget(Vector3 myPos)
        {
            float aggroSq = AggroRange * AggroRange;
            float loseSq = LoseAggroRange * LoseAggroRange;

            // 已有仇恨：检查是否仍在脱战范围内
            if (_hasAggro && _targetPlayerId >= 0)
            {
                ActPlayer kept = FindPlayerById(_targetPlayerId);
                if (kept != null)
                {
                    float dsq = HorizSq(myPos, kept.Position);
                    if (dsq <= loseSq)
                        return kept;
                }
                // 丢失或超距 → 清仇恨
                _hasAggro = false;
                _targetPlayerId = -1;
            }

            // 重新搜最近玩家
            ActPlayer best = null;
            float bestSq = aggroSq;
            foreach (var p in EntityManager.GetEntities<ActPlayer>())
            {
                if (p == null || p.IsDestroyed) continue;
                float dsq = HorizSq(myPos, p.Position);
                if (dsq < bestSq)
                {
                    bestSq = dsq;
                    best = p;
                }
            }
            return best;
        }

        ActPlayer FindPlayerById(int id)
        {
            foreach (var p in EntityManager.GetEntities<ActPlayer>())
            {
                if (p != null && !p.IsDestroyed && p.Id == id)
                    return p;
            }
            return null;
        }

        void Chase(ActMonster pawn, Vector3 targetPos, float dt)
        {
            Vector3 myPos = pawn.Position;
            Vector3 toTarget = targetPos - myPos;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;

            Vector3 dir;
            // FlowField 优先（绕障）；无场或采样为零则直线
            Vector3 flow = FlowFieldService.SampleDirection(myPos);
            if (flow.sqrMagnitude > 1e-4f)
                dir = flow;
            else if (dist > 0.001f)
                dir = toTarget / dist;
            else
                dir = Vector3.zero;

            // 近身：停步、面向目标
            if (dist <= MeleeRange)
            {
                dir = Vector3.zero;
            }

            float targetYaw;
            if (toTarget.sqrMagnitude > 1e-4f)
                targetYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            else
                targetYaw = pawn.Yaw;

            _yaw = Mathf.MoveTowardsAngle(_yaw, targetYaw, 240f * dt);
            pawn.SetInput(dir, _yaw);
        }

        void IdleWander(ActMonster pawn, float dt)
        {
            _changeTimer -= dt;
            if (_changeTimer <= 0f)
            {
                _yaw += Random.Range(-55f, 55f);
                _changeTimer = Random.Range(IdleTurnMin, IdleTurnMax);
            }

            // 偶发停顿
            bool idle = Random.Range(0, 50) == 0;
            Vector3 dir = Vector3.zero;
            if (!idle)
            {
                float rad = _yaw * Mathf.Deg2Rad;
                dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            }
            pawn.SetInput(dir, _yaw);
        }

        static float HorizSq(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
