using System.Collections.Generic;
using UnityEngine;

namespace GameAct.Spatial
{
    /// <summary>
    /// 怪物 Local Avoidance：分离力（Separation）+ 时间平滑。
    ///
    /// 围殴稳定策略：
    /// - 追击途中：全半径 soft separation，避免路上挤成一团
    /// - 已进近战圈：只用硬核（真正重叠）推开，其余站定朝向目标
    ///   → 形成稳定围圈，而不是一直侧向滑位换边
    /// </summary>
    public static class LocalAvoidance
    {
        /// <summary>追击时 soft 排斥半径（米）</summary>
        public static float SeparationRadius = 1.45f;

        /// <summary>与期望方向混合的默认权重</summary>
        public static float SeparationWeight = 0.85f;

        /// <summary>真正重叠才强推的硬核半径</summary>
        public static float HardRadius = 0.70f;

        /// <summary>硬核内额外倍率（平滑）</summary>
        public static float CloseBoost = 1.4f;

        /// <summary>分离向量最大长度</summary>
        public static float MaxSeparationMag = 1.15f;

        /// <summary>最多邻居数</summary>
        public static int MaxNeighbors = 10;

        /// <summary>EMA 时间常数（秒）</summary>
        public static float SmoothTime = 0.12f;

        /// <summary>
        /// 计算水平分离力。
        /// hardOnly=true 时只统计 HardRadius 内邻居（围殴站位用，避免软半径侧向滑）。
        /// </summary>
        public static Vector3 ComputeSeparation(
            Vector3 selfPos,
            IList<Vector3> neighborPositions,
            bool hardOnly = false)
        {
            if (neighborPositions == null || neighborPositions.Count == 0)
                return Vector3.zero;

            float hard = Mathf.Max(0.08f, HardRadius);
            float radius = hardOnly
                ? hard
                : Mathf.Max(hard, SeparationRadius);
            float radiusSq = radius * radius;
            float hardSq = hard * hard;
            float invRadius = 1f / radius;

            Vector3 push = Vector3.zero;
            int used = 0;

            for (int i = 0; i < neighborPositions.Count; i++)
            {
                if (used >= MaxNeighbors) break;

                Vector3 delta = selfPos - neighborPositions[i];
                delta.y = 0f;
                float dsq = delta.sqrMagnitude;

                if (dsq < 1e-8f)
                {
                    float ang = i * 2.399963f;
                    push += new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * CloseBoost;
                    used++;
                    continue;
                }

                if (dsq > radiusSq) continue;
                // hardOnly 已用 radius=hard，这里再保险一层
                if (hardOnly && dsq > hardSq) continue;

                float d = Mathf.Sqrt(dsq);
                Vector3 away = delta / d;

                float t = 1f - (d * invRadius);
                t = t * t * (3f - 2f * t); // smoothstep

                float strength = t;
                if (d < hard)
                {
                    float h = 1f - (d / hard);
                    strength *= 1f + (CloseBoost - 1f) * h;
                }

                push += away * strength;
                used++;
            }

            float mag = push.magnitude;
            if (mag > MaxSeparationMag)
                push *= MaxSeparationMag / mag;

            return push;
        }

        public static Vector3 SmoothSeparation(Vector3 prev, Vector3 raw, float dt)
        {
            float st = Mathf.Max(0.01f, SmoothTime);
            float alpha = 1f - Mathf.Exp(-Mathf.Max(0f, dt) / st);
            Vector3 s = Vector3.Lerp(prev, raw, alpha);
            s.y = 0f;
            return s;
        }

        /// <summary>
        /// 期望方向 + 分离 → 水平单位方向。
        /// 当 desired≈0 且分离很弱时返回 zero（站定）。
        /// </summary>
        public static Vector3 Blend(Vector3 desiredDir, Vector3 separation, float weight = -1f)
        {
            if (weight < 0f) weight = SeparationWeight;

            desiredDir.y = 0f;
            separation.y = 0f;

            Vector3 v = Vector3.zero;
            if (desiredDir.sqrMagnitude > 1e-6f)
                v = desiredDir.normalized;

            if (separation.sqrMagnitude > 1e-8f)
                v += separation * weight;

            v.y = 0f;
            float sq = v.sqrMagnitude;
            if (sq < 1e-8f)
                return Vector3.zero;
            return v / Mathf.Sqrt(sq);
        }

        /// <summary>
        /// 限角速度转向。target 为零时衰减 current → 零（刹车），避免原地打转。
        /// </summary>
        public static Vector3 SteerTowards(Vector3 current, Vector3 target, float maxRadPerSec, float dt)
        {
            current.y = 0f;
            target.y = 0f;

            // 目标停下：快速衰减而不是保持旧方向滑行
            if (target.sqrMagnitude < 1e-8f)
            {
                if (current.sqrMagnitude < 1e-8f)
                    return Vector3.zero;
                float decay = Mathf.Exp(-Mathf.Max(0f, dt) / 0.08f);
                Vector3 damped = current * decay;
                return damped.sqrMagnitude < 1e-4f ? Vector3.zero : damped;
            }

            target.Normalize();

            if (current.sqrMagnitude < 1e-8f)
                return target;

            current.Normalize();
            float maxAngle = Mathf.Max(0f, maxRadPerSec) * Mathf.Max(0f, dt);
            float angle = Vector3.SignedAngle(current, target, Vector3.up) * Mathf.Deg2Rad;
            float step = Mathf.Clamp(angle, -maxAngle, maxAngle);
            if (Mathf.Abs(step) < 1e-6f)
                return target;

            return (Quaternion.AngleAxis(step * Mathf.Rad2Deg, Vector3.up) * current).normalized;
        }
    }
}
