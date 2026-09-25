using System.Collections.Generic;
using UnityEngine;

namespace GameAct.Spatial
{
    /// <summary>
    /// 怪物 Local Avoidance：分离力（Separation）最简版。
    /// 不依赖 NavMesh/RVO；与 FlowField 期望方向混合即可。
    /// </summary>
    public static class LocalAvoidance
    {
        /// <summary>开始产生排斥的半径（米）</summary>
        public static float SeparationRadius = 1.35f;

        /// <summary>分离力权重（与期望方向混合）</summary>
        public static float SeparationWeight = 1.4f;

        /// <summary>贴身时额外加强（形成围殴圈、减少重叠）</summary>
        public static float CloseBoost = 2.2f;

        /// <summary>认为「几乎重叠」的距离，强化侧向推开</summary>
        public static float HardRadius = 0.55f;

        /// <summary>最多统计邻居数（性能）</summary>
        public static int MaxNeighbors = 12;

        /// <summary>
        /// 计算水平分离力（未归一化）。positions 为邻居世界坐标。
        /// </summary>
        public static Vector3 ComputeSeparation(Vector3 selfPos, IList<Vector3> neighborPositions)
        {
            if (neighborPositions == null || neighborPositions.Count == 0)
                return Vector3.zero;

            float radius = Mathf.Max(0.1f, SeparationRadius);
            float radiusSq = radius * radius;
            float hard = Mathf.Max(0.05f, HardRadius);
            float hardSq = hard * hard;

            Vector3 push = Vector3.zero;
            int used = 0;

            for (int i = 0; i < neighborPositions.Count; i++)
            {
                if (used >= MaxNeighbors) break;

                Vector3 delta = selfPos - neighborPositions[i];
                delta.y = 0f;
                float dsq = delta.sqrMagnitude;
                if (dsq > radiusSq || dsq < 1e-10f) continue;

                float d = Mathf.Sqrt(dsq);
                Vector3 away = delta / d;

                // 距离越近力越大；硬半径内额外放大
                float t = 1f - (d / radius);
                float strength = t * t;
                if (dsq < hardSq)
                    strength *= CloseBoost * (1f + (hard - d) / hard);

                push += away * strength;
                used++;
            }

            return push;
        }

        /// <summary>
        /// 期望方向 + 分离力 → 最终水平单位方向（可为零）。
        /// </summary>
        public static Vector3 Blend(Vector3 desiredDir, Vector3 separation, float weight = -1f)
        {
            if (weight < 0f) weight = SeparationWeight;

            desiredDir.y = 0f;
            separation.y = 0f;

            Vector3 v = desiredDir;
            if (desiredDir.sqrMagnitude > 1e-6f)
                v = desiredDir.normalized;

            if (separation.sqrMagnitude > 1e-6f)
                v += separation * weight;

            v.y = 0f;
            if (v.sqrMagnitude < 1e-8f)
                return Vector3.zero;
            return v.normalized;
        }
    }
}
