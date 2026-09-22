using UnityEngine;

namespace GameAct.Spatial
{
    public enum LogicShapeType
    {
        AABB,
        Sphere,
        Capsule
    }

    /// <summary>
    /// Single logic collider definition (local space relative to entity root).
    /// Can be used for body, head, weak point, etc.
    /// </summary>
    [System.Serializable]
    public struct LogicColliderData
    {
        public string Name;                     // e.g. "Body", "Head", "WeakPoint"
        public LogicShapeType Shape;

        [Tooltip("Local center offset from entity root")]
        public Vector3 CenterOffset;

        // AABB
        public Vector3 Size;                    // for AABB

        // Sphere / Capsule
        public float Radius;

        // Capsule
        public float Height;                    // total height for capsule
        public int Direction;                   // 0=X, 1=Y, 2=Z (Unity Capsule style)

        public static LogicColliderData DefaultBody()
        {
            return new LogicColliderData
            {
                Name = "Body",
                Shape = LogicShapeType.AABB,
                CenterOffset = new Vector3(0f, 1f, 0f),
                Size = new Vector3(0.8f, 2f, 0.8f),
                Radius = 0.4f,
                Height = 2f,
                Direction = 1
            };
        }

        /// <summary>
        /// Convert this local definition to a world-space AABB
        /// (conservative bounds, good enough for Spatial broadphase).
        /// </summary>
        public AABB ToWorldAABB(Vector3 worldPos, Quaternion worldRot)
        {
            Vector3 worldCenter = worldPos + worldRot * CenterOffset;

            switch (Shape)
            {
                case LogicShapeType.Sphere:
                    {
                        Vector3 ext = Vector3.one * Radius;
                        return AABB.FromCenterExtents(worldCenter, ext);
                    }
                case LogicShapeType.Capsule:
                    {
                        // Approximate capsule as AABB
                        float halfH = Mathf.Max(0f, Height * 0.5f - Radius);
                        Vector3 ext;
                        if (Direction == 0)      // X
                            ext = new Vector3(halfH + Radius, Radius, Radius);
                        else if (Direction == 2) // Z
                            ext = new Vector3(Radius, Radius, halfH + Radius);
                        else                     // Y (default)
                            ext = new Vector3(Radius, halfH + Radius, Radius);

                        // Rotate extents approximately (axis-aligned after rotation is complex,
                        // for prototype we use a simple enlarged AABB)
                        float maxExt = Mathf.Max(ext.x, ext.y, ext.z);
                        return AABB.FromCenterExtents(worldCenter, Vector3.one * maxExt);
                    }
                case LogicShapeType.AABB:
                default:
                    {
                        // Rotate the local size (approximate OBB → AABB)
                        Vector3 half = Size * 0.5f;
                        Vector3 right = worldRot * new Vector3(half.x, 0, 0);
                        Vector3 up    = worldRot * new Vector3(0, half.y, 0);
                        Vector3 fwd   = worldRot * new Vector3(0, 0, half.z);

                        Vector3 worldHalf = new Vector3(
                            Mathf.Abs(right.x) + Mathf.Abs(up.x) + Mathf.Abs(fwd.x),
                            Mathf.Abs(right.y) + Mathf.Abs(up.y) + Mathf.Abs(fwd.y),
                            Mathf.Abs(right.z) + Mathf.Abs(up.z) + Mathf.Abs(fwd.z)
                        );

                        return AABB.FromCenterExtents(worldCenter, worldHalf);
                    }
            }
        }
    }
}