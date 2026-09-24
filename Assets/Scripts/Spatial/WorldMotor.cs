using UnityEngine;

namespace GameAct.Spatial
{
    /// <summary>
    /// 极简场景阻挡：只挡「墙」，不挡「可走坡」。
    /// 水平：胸口高度 SphereCast，仅当法线偏竖（墙）才截断位移；不做多段滑墙（避免来回弹）。
    /// 竖直：一次向下 Raycast 贴地。
    /// </summary>
    public static class WorldMotor
    {
        public const float DefaultRadius = 0.35f;
        public const float DefaultHeight = 1.75f;
        public const float Skin = 0.05f;
        public const float SnapProbeUp = 3f;
        public const float SnapProbeDown = 8f;
        public const float GroundOffset = 0.05f;

        /// <summary>法线.y ≥ 此值 = 地面/缓坡，水平不挡。</summary>
        public static float MinGroundNormalY = 0.4f;

        /// <summary>胸口探测高度（相对脚底），避开脚扫到坡面。</summary>
        public static float ChestHeight = 0.95f;

        public static int EnvironmentMask { get; set; } = Physics.DefaultRaycastLayers;

        public static Vector3 MoveHorizontalAndSnap(
            Vector3 position,
            Vector3 horizontalDelta,
            ref float velY,
            ref bool grounded,
            float radius = DefaultRadius,
            float height = DefaultHeight,
            int layerMask = -1)
        {
            if (layerMask < 0) layerMask = EnvironmentMask;
            horizontalDelta.y = 0f;

            position = BlockWallsOnly(position, horizontalDelta, radius, layerMask);
            position = SnapToGround(position, ref velY, ref grounded, layerMask);
            return position;
        }

        public static Vector3 MoveHorizontalAndSnap(
            Vector3 position,
            Vector3 horizontalDelta,
            float radius = DefaultRadius,
            float height = DefaultHeight,
            int layerMask = -1)
        {
            float velY = 0f;
            bool grounded = true;
            return MoveHorizontalAndSnap(position, horizontalDelta, ref velY, ref grounded, radius, height, layerMask);
        }

        /// <summary>
        /// 只挡墙 + 贴墙滑动。
        /// 1) 起点后退 Back，避免"已经贴墙 = 初始重叠"时 SphereCast 返回 distance=0、normal=-dir 的假法线。
        /// 2) 撞墙后把剩余位移投影到墙切面继续走（最多 MaxIter 次），不再整段丢弃。
        /// 3) 与墙的夹角是"平行/背离"时直接放行。
        /// 4) 若已嵌入 Skin 内，只轻推出 ≤Skin，不会像深度穿透修正那样弹。
        /// </summary>
        public static Vector3 BlockWallsOnly(Vector3 position, Vector3 delta, float radius, int layerMask)
        {
            delta.y = 0f;
            const int MaxIter = 3;
            const float Back = 0.05f;
            float r = radius * 0.85f;
            Vector3 firstDir = delta.sqrMagnitude > 1e-12f ? delta.normalized : Vector3.zero;

            for (int i = 0; i < MaxIter; i++)
            {
                float dist = delta.magnitude;
                if (dist < 1e-6f) break;

                Vector3 dir = delta / dist;
                Vector3 origin = position + Vector3.up * ChestHeight;

                if (!Physics.SphereCast(
                        origin - dir * Back, r, dir,
                        out RaycastHit hit, dist + Back + Skin,
                        layerMask, QueryTriggerInteraction.Ignore))
                {
                    position += delta;
                    break;
                }

                // 取法线：初始重叠时 hit.normal 不可信，用最近点重算
                Vector3 n = hit.normal;
                if (hit.distance <= 0f && hit.collider != null)
                {
                    Vector3 d = origin - hit.collider.ClosestPoint(origin);
                    if (d.sqrMagnitude > 1e-8f) n = d.normalized;
                }

                // 地面/缓坡：放行，高度交给 Snap
                if (n.y >= MinGroundNormalY)
                {
                    position += delta;
                    break;
                }

                n.y = 0f;
                if (n.sqrMagnitude < 1e-6f) break;
                n.Normalize();

                // 平行或背离墙：不算阻挡
                if (Vector3.Dot(dir, n) >= -0.001f)
                {
                    position += delta;
                    break;
                }

                // 走到接触点前
                float gap = hit.distance - Back;                 // 球面到墙面的空隙(相对 Skin 的余量另算)
                float travel = Mathf.Max(0f, gap - Skin);
                position += dir * travel;

                // 已嵌入 Skin 内：轻推出，最多 Skin
                float embed = Skin - gap;
                if (embed > 0f)
                    position += n * Mathf.Min(embed, Skin);

                // 剩余位移投影到墙切面 → 滑动
                Vector3 remain = dir * (dist - travel);
                delta = remain - n * Vector3.Dot(remain, n);

                // 夹角/拐角保护：滑动方向与最初意图相反就停（避免来回抖）
                if (Vector3.Dot(delta, firstDir) <= 0f) break;
            }

            return position;
        }

        // 兼容旧名
        public static Vector3 SlideMove(Vector3 position, Vector3 delta, float radius, float height, int layerMask)
            => BlockWallsOnly(position, delta, radius, layerMask);

        public static Vector3 SnapToGround(
            Vector3 position,
            ref float velY,
            ref bool grounded,
            int layerMask = -1)
        {
            if (layerMask < 0) layerMask = EnvironmentMask;

            Vector3 origin = position + Vector3.up * SnapProbeUp;
            float maxDist = SnapProbeUp + SnapProbeDown;

            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDist, layerMask, QueryTriggerInteraction.Ignore))
            {
                grounded = false;
                return position;
            }

            // 打到墙侧面：不乱吸
            if (hit.normal.y < MinGroundNormalY * 0.5f)
            {
                grounded = false;
                return position;
            }

            float footY = hit.point.y + GroundOffset;

            // 上升中且明显离地：不 Snap（跳跃）
            if (velY > 0.5f && position.y > footY + 0.2f)
            {
                grounded = false;
                return position;
            }

            // 抑制贴地抖动：误差很小就不改 Y
            float dy = footY - position.y;
            if (Mathf.Abs(dy) < 0.001f)
            {
                grounded = true;
                if (velY < 0f) velY = 0f;
                return position;
            }

            // 上坡：允许一帧抬高/下降；限制单帧最大变化，减少镜头抖
            const float maxStep = 0.55f;
            if (dy > maxStep) dy = maxStep;
            if (dy < -maxStep) dy = -maxStep;

            position.y += dy;
            grounded = true;
            if (velY < 0f) velY = 0f;
            return position;
        }

        public static Vector3 SnapToGround(Vector3 position, int layerMask = -1)
        {
            float velY = 0f;
            bool grounded = true;
            return SnapToGround(position, ref velY, ref grounded, layerMask);
        }
    }
}
