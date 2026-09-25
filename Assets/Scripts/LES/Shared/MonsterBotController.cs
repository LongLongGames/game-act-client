using System.Collections.Generic;
using LiteEntitySystem;
using UnityEngine;
using GameAct.Spatial;

namespace GameAct.Les.Shared
{
    /// <summary>
    /// 怪物基础 AI：
    /// - Idle 游荡 + Local Avoidance
    /// - 仇恨范围进入 → Chase（FlowField / 直线）+ Local Avoidance
    /// - 近战距离停步但仍侧向分离，形成围圈而非重叠
    /// </summary>
    public class MonsterBotController : AiControllerLogic<ActMonster>
    {
        /// <summary>进入仇恨的半径（米）</summary>
        public static float AggroRange = 10f;
        /// <summary>脱战半径（米），应大于 AggroRange</summary>
        public static float LoseAggroRange = 15f;
        /// <summary>认为“贴身”可攻击的距离</summary>
        public static float MeleeRange = 1.6f;
        /// <summary>游荡转向间隔</summary>
        public static float IdleTurnMin = 0.7f;
        public static float IdleTurnMax = 2.2f;

        /// <summary>是否启用邻居分离</summary>
        public static bool EnableAvoidance = true;

        float _yaw;
        float _changeTimer;
        int _targetPlayerId = -1;
        Vector3 _lastKnownTargetPos;
        bool _hasAggro;

        // 复用列表，避免每帧 GC
        static readonly List<Vector3> _neighborBuf = new List<Vector3>(16);

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
                // 丢失或超距 → 清仇恨
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
                // 丢失或超距 → 清仇恨
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

            Vector3 desired;
            Vector3 flow = FlowFieldService.SampleDirection(myPos);
            if (flow.sqrMagnitude > 1e-4f)
                desired = flow;
            else if (dist > 0.001f)
                desired = toTarget / dist;
            else
                desired = Vector3.zero;

            // 进近战：不再前冲，只保留分离/侧移，避免叠成一团
            if (dist <= MeleeRange)
                desired = Vector3.zero;

            if (EnableAvoidance)
            {
                CollectNeighborPositions(pawn, myPos);
                Vector3 sep = LocalAvoidance.ComputeSeparation(myPos, _neighborBuf);
                float w = LocalAvoidance.SeparationWeight;
                if (dist <= MeleeRange * 1.25f)
                    w *= 1.5f;
                desired = LocalAvoidance.Blend(desired, sep, w);
            }
            else if (desired.sqrMagnitude > 1e-6f)
            {
                desired.Normalize();
            }

            float targetYaw;
            if (toTarget.sqrMagnitude > 1e-4f)
                targetYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            else if (desired.sqrMagnitude > 1e-4f)
                targetYaw = Mathf.Atan2(desired.x, desired.z) * Mathf.Rad2Deg;
            else
                targetYaw = pawn.Yaw;

            _yaw = Mathf.MoveTowardsAngle(_yaw, targetYaw, 240f * dt);
            pawn.SetInput(desired, _yaw);
        }

        void IdleWander(ActMonster pawn, float dt)
        {
            _changeTimer -= dt;
            if (_changeTimer <= 0f)
            {
                _yaw += Random.Range(-55f, 55f);
                _changeTimer = Random.Range(IdleTurnMin, IdleTurnMax);
            }

            bool idle = Random.Range(0, 50) == 0;
            Vector3 desired = Vector3.zero;
            if (!idle)
            {
                float rad = _yaw * Mathf.Deg2Rad;
                desired = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            }

            if (EnableAvoidance)
            {
                CollectNeighborPositions(pawn, pawn.Position);
                Vector3 sep = LocalAvoidance.ComputeSeparation(pawn.Position, _neighborBuf);
                desired = LocalAvoidance.Blend(desired, sep, LocalAvoidance.SeparationWeight);
                if (desired.sqrMagnitude > 1e-4f)
                    _yaw = Mathf.MoveTowardsAngle(
                        _yaw,
                        Mathf.Atan2(desired.x, desired.z) * Mathf.Rad2Deg,
                        180f * dt);
            }

            pawn.SetInput(desired, _yaw);
        }

        void CollectNeighborPositions(ActMonster self, Vector3 myPos)
        {
            _neighborBuf.Clear();
            float r = LocalAvoidance.SeparationRadius;
            float rSq = r * r;
            int selfId = self.Id;

            foreach (var m in EntityManager.GetEntities<ActMonster>())
            {
                if (m == null || m.IsDestroyed || m.IsDead) continue;
                if (m.Id == selfId) continue;
                float dsq = HorizSq(myPos, m.Position);
                if (dsq > rSq) continue;
                _neighborBuf.Add(m.Position);
                if (_neighborBuf.Count >= LocalAvoidance.MaxNeighbors)
                    break;
            }
        }

        static float HorizSq(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
