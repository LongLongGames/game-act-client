using System.Collections.Generic;
using LiteEntitySystem;
using UnityEngine;
using GameAct.Spatial;

namespace GameAct.Les.Shared
{
    /// <summary>
    /// 怪物基础 AI：
    /// - Idle 游荡 + Local Avoidance
    /// - Chase：FlowField / 直线 + 途中 soft 分离
    /// - 近战圈内：站定朝向目标，仅在真正重叠时硬推开（不再侧向换位乱晃）
    /// </summary>
    public class MonsterBotController : AiControllerLogic<ActMonster>
    {
        public static float AggroRange = 10f;
        public static float LoseAggroRange = 15f;
        public static float MeleeRange = 1.6f;
        public static float IdleTurnMin = 0.7f;
        public static float IdleTurnMax = 2.2f;

        public static bool EnableAvoidance = true;

        /// <summary>追击途中最终方向最大转向角速度（度/秒）</summary>
        public static float MaxSteerDegPerSec = 320f;

        /// <summary>
        /// 近战站位环：略小于 MeleeRange。
        /// 在 HoldBand..MeleeRange 之间且未重叠 → 完全站定。
        /// </summary>
        public static float HoldBand = 1.15f;

        float _yaw;
        float _changeTimer;
        int _targetPlayerId = -1;
        Vector3 _lastKnownTargetPos;
        bool _hasAggro;

        readonly List<Vector3> _neighborBuf = new List<Vector3>(16);

        Vector3 _sepSmoothed;
        Vector3 _lastDesired;

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
                _sepSmoothed = Vector3.zero;
                _lastDesired = Vector3.zero;
                return;
            }

            float dt = EntityManager.DeltaTimeF;
            Vector3 myPos = pawn.Position;

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

            if (_hasAggro && _targetPlayerId >= 0)
            {
                ActPlayer kept = FindPlayerById(_targetPlayerId);
                if (kept != null)
                {
                    float dsq = HorizSq(myPos, kept.Position);
                    if (dsq <= loseSq)
                        return kept;
                }
                _hasAggro = false;
                _targetPlayerId = -1;
            }

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

            // 始终优先面向目标（围殴时不跟着分离力扭头乱转）
            float targetYaw = pawn.Yaw;
            if (toTarget.sqrMagnitude > 1e-4f)
                targetYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;

            Vector3 desired;

            // ── 已进入近战环：站定为主，只处理真正重叠 ──
            if (dist <= MeleeRange)
            {
                desired = Vector3.zero;

                if (EnableAvoidance)
                {
                    CollectNeighborPositions(pawn, myPos, hardOnlyQuery: true);
                    Vector3 sepRaw = LocalAvoidance.ComputeSeparation(myPos, _neighborBuf, hardOnly: true);
                    _sepSmoothed = LocalAvoidance.SmoothSeparation(_sepSmoothed, sepRaw, dt);

                    // 只有硬核分离足够强才允许挪一步，否则完全站定
                    if (_sepSmoothed.sqrMagnitude > 0.04f)
                    {
                        // 权重偏低：只挤开重叠，不绕圈换位
                        desired = LocalAvoidance.Blend(Vector3.zero, _sepSmoothed, 0.55f);
                    }
                    else
                    {
                        _sepSmoothed = Vector3.Lerp(_sepSmoothed, Vector3.zero, 1f - Mathf.Exp(-dt / 0.06f));
                        desired = Vector3.zero;
                    }
                }

                // 刹车：目标方向为零时衰减旧速度感
                desired = LocalAvoidance.SteerTowards(
                    _lastDesired, desired,
                    MaxSteerDegPerSec * Mathf.Deg2Rad, dt);

                _lastDesired = desired;
                _yaw = Mathf.MoveTowardsAngle(_yaw, targetYaw, 280f * dt);
                pawn.SetInput(desired, _yaw);
                return;
            }

            // ── 追击途中：FlowField / 直线 + soft 分离 ──
            Vector3 flow = FlowFieldService.SampleDirection(myPos);
            if (flow.sqrMagnitude > 1e-4f)
                desired = flow;
            else if (dist > 0.001f)
                desired = toTarget / dist;
            else
                desired = Vector3.zero;

            if (EnableAvoidance)
            {
                CollectNeighborPositions(pawn, myPos, hardOnlyQuery: false);
                Vector3 sepRaw = LocalAvoidance.ComputeSeparation(myPos, _neighborBuf, hardOnly: false);
                _sepSmoothed = LocalAvoidance.SmoothSeparation(_sepSmoothed, sepRaw, dt);

                // 越接近近战环，分离权重越低，减少“抢位侧滑”
                float approach = Mathf.InverseLerp(MeleeRange, MeleeRange * 2.5f, dist);
                float w = LocalAvoidance.SeparationWeight * Mathf.Lerp(0.35f, 1f, approach);

                desired = LocalAvoidance.Blend(desired, _sepSmoothed, w);
                desired = LocalAvoidance.SteerTowards(
                    _lastDesired, desired,
                    MaxSteerDegPerSec * Mathf.Deg2Rad, dt);
            }
            else if (desired.sqrMagnitude > 1e-6f)
            {
                desired.Normalize();
            }

            _lastDesired = desired;
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

            float rad = _yaw * Mathf.Deg2Rad;
            Vector3 desired = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

            if (EnableAvoidance)
            {
                CollectNeighborPositions(pawn, pawn.Position, hardOnlyQuery: false);
                Vector3 sepRaw = LocalAvoidance.ComputeSeparation(pawn.Position, _neighborBuf, hardOnly: false);
                _sepSmoothed = LocalAvoidance.SmoothSeparation(_sepSmoothed, sepRaw, dt);
                desired = LocalAvoidance.Blend(desired, _sepSmoothed, LocalAvoidance.SeparationWeight);
                desired = LocalAvoidance.SteerTowards(
                    _lastDesired, desired,
                    MaxSteerDegPerSec * Mathf.Deg2Rad, dt);

                if (desired.sqrMagnitude > 1e-4f)
                {
                    _yaw = Mathf.MoveTowardsAngle(
                        _yaw,
                        Mathf.Atan2(desired.x, desired.z) * Mathf.Rad2Deg,
                        180f * dt);
                }
            }

            _lastDesired = desired;
            pawn.SetInput(desired, _yaw);
        }

        /// <summary>
        /// hardOnlyQuery：近战站位时用更小查询半径，少收集“远邻”减少无效侧向力。
        /// </summary>
        void CollectNeighborPositions(ActMonster self, Vector3 myPos, bool hardOnlyQuery)
        {
            _neighborBuf.Clear();
            float r = hardOnlyQuery
                ? LocalAvoidance.HardRadius * 1.05f
                : LocalAvoidance.SeparationRadius;
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
