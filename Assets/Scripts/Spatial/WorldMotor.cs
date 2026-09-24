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
        /// 只挡墙：胸口 SphereCast。命中可走坡 → 当没撞到，整段位移放行。
        /// 命中墙 → 停在墙前，不 Project 滑墙（避免弹）。
        /// </summary>
        public static Vector3 BlockWallsOnly(Vector3 position, Vector3 delta, float radius, int layerMask)
        {
            delta.y = 0f;
            float dist = delta.magnitude;
            if (dist < 1e-7f)
                return position;

            Vector3 dir = delta / dist;
            Vector3 origin = position + Vector3.up * ChestHeight;
            float castDist = dist + Skin;

            if (!Physics.SphereCast(
                    origin, radius * 0.85f, dir,
                    out RaycastHit hit, castDist,
                    layerMask, QueryTriggerInteraction.Ignore))
            {
                return position + delta;
            }

            // 缓坡/地面：直接走过去，高度交给 Snap
            if (hit.normal.y >= MinGroundNormalY)
                return position + delta;

            // 墙：停在接触点前，不滑、不推回
            float travel = Mathf.Max(0f, hit.distance - Skin);
            return position + dir * travel;
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
