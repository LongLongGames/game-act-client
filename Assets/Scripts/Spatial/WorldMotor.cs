using UnityEngine;

namespace GameAct.Spatial
{
    /// <summary>
    /// 极简场景阻挡：只挡「墙」，不挡「可走坡」。
    /// 水平：胸口高度 SphereCast，仅当法线偏竖（墙）才截断位移。
    /// 竖直：向下 Raycast 贴地；下落时只做近地吸附，不整段传送。
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

        /// <summary>
        /// 距地面超过此值时，下落中不 Snap，交给重力慢慢掉。
        /// 小于此值才吸附，避免穿地 / 落地弹。
        /// </summary>
        public static float FallSnapDistance = 0.35f;

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

                Vector3 n = hit.normal;
                if (hit.distance <= 0f && hit.collider != null)
                {
                    Vector3 d = origin - hit.collider.ClosestPoint(origin);
                    if (d.sqrMagnitude > 1e-8f) n = d.normalized;
                }

                if (n.y >= MinGroundNormalY)
                {
                    position += delta;
                    break;
                }

                n.y = 0f;
                if (n.sqrMagnitude < 1e-6f) break;
                n.Normalize();

                if (Vector3.Dot(dir, n) >= -0.001f)
                {
                    position += delta;
                    break;
                }

                float gap = hit.distance - Back;
                float travel = Mathf.Max(0f, gap - Skin);
                position += dir * travel;

                float embed = Skin - gap;
                if (embed > 0f)
                    position += n * Mathf.Min(embed, Skin);

                Vector3 remain = dir * (dist - travel);
                delta = remain - n * Vector3.Dot(remain, n);

                if (Vector3.Dot(delta, firstDir) <= 0f) break;
            }

            return position;
        }

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

            // 打到墙侧面：不吸，保持离地
            if (hit.normal.y < MinGroundNormalY * 0.5f)
            {
                grounded = false;
                return position;
            }

            float footY = hit.point.y + GroundOffset;
            float dy = footY - position.y;

            // 上升中且明显离地：不 Snap（跳跃）
            if (velY > 0.5f && position.y > footY + 0.2f)
            {
                grounded = false;
                return position;
            }

            // 已贴地：微小误差忽略
            if (Mathf.Abs(dy) < 0.001f)
            {
                grounded = true;
                if (velY < 0f) velY = 0f;
                return position;
            }

            // ── 下落 / 离地：只做近地吸附，禁止大段传送 ──
            // dy < 0 → 脚在地面上方，需要往下靠
            if (dy < 0f)
            {
                float gap = -dy; // 离地高度

                // 离地超过 FallSnapDistance：纯重力下落，本帧不改 Y
                if (!grounded && gap > FallSnapDistance)
                {
                    grounded = false;
                    return position;
                }

                // 近地（或上一帧还在地上走坡）：小步贴地，防止穿地
                const float maxDownWalk = 0.55f;   // 走坡/上台阶反向的下降
                const float maxDownLand = 0.4f;    // 落地吸附上限（略大于 FallSnapDistance）
                float maxDown = grounded ? maxDownWalk : maxDownLand;
                if (dy < -maxDown) dy = -maxDown;

                position.y += dy;
                grounded = true;
                if (velY < 0f) velY = 0f;
                return position;
            }

            // ── 需要抬升（上坡）──
            const float maxUp = 0.55f;
            if (dy > maxUp) dy = maxUp;
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
