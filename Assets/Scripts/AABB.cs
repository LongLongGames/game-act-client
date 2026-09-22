using UnityEngine;

namespace GameAct.Spatial
{
    /// <summary>
    /// Simple Axis-Aligned Bounding Box.
    /// Kept as a struct for performance and Burst compatibility later.
    /// </summary>
    [System.Serializable]
    public struct AABB
    {
        public Vector3 Min;
        public Vector3 Max;

        public Vector3 Center => (Min + Max) * 0.5f;
        public Vector3 Size => Max - Min;
        public Vector3 Extents => Size * 0.5f;

        public AABB(Vector3 min, Vector3 max)
        {
            Min = min;
            Max = max;
        }

        public AABB(Vector3 center, Vector3 extents, bool isExtents)
        {
            if (isExtents)
            {
                Min = center - extents;
                Max = center + extents;
            }
            else
            {
                // treat second param as size
                Min = center - extents * 0.5f;
                Max = center + extents * 0.5f;
            }
        }

        public static AABB FromCenterSize(Vector3 center, Vector3 size)
        {
            var half = size * 0.5f;
            return new AABB(center - half, center + half);
        }

        public static AABB FromCenterExtents(Vector3 center, Vector3 extents)
        {
            return new AABB(center - extents, center + extents);
        }

        public bool Overlaps(in AABB other)
        {
            return Min.x <= other.Max.x && Max.x >= other.Min.x &&
                   Min.y <= other.Max.y && Max.y >= other.Min.y &&
                   Min.z <= other.Max.z && Max.z >= other.Min.z;
        }

        public bool Contains(Vector3 point)
        {
            return point.x >= Min.x && point.x <= Max.x &&
                   point.y >= Min.y && point.y <= Max.y &&
                   point.z >= Min.z && point.z <= Max.z;
        }

        public float DistanceSqr(Vector3 point)
        {
            float dx = Mathf.Max(Min.x - point.x, 0f, point.x - Max.x);
            float dy = Mathf.Max(Min.y - point.y, 0f, point.y - Max.y);
            float dz = Mathf.Max(Min.z - point.z, 0f, point.z - Max.z);
            return dx * dx + dy * dy + dz * dz;
        }

        public void Encapsulate(Vector3 point)
        {
            Min = Vector3.Min(Min, point);
            Max = Vector3.Max(Max, point);
        }

        public void Expand(float amount)
        {
            Min -= Vector3.one * amount;
            Max += Vector3.one * amount;
        }
    }
}