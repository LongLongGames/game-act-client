using System.Collections.Generic;
using UnityEngine;

namespace GameAct.Spatial
{
    /// <summary>
    /// Core spatial index interface.
    /// All higher systems (skills, AI, AOI, avoidance) depend only on this.
    /// Implementations can be swapped freely (SpatialHash → Quadtree → Native/Burst → C++ plugin).
    /// </summary>
    public interface ISpatialIndex
    {
        /// <summary>Clear all entries.</summary>
        void Clear();

        /// <summary>
        /// Insert or update an entity.
        /// If the entity already exists, it will be updated to the new bounds.
        /// </summary>
        void Insert(int entityId, in AABB bounds, SpatialLayer layer = SpatialLayer.Ground);

        /// <summary>Remove an entity. Safe to call if not present.</summary>
        void Remove(int entityId);

        /// <summary>
        /// Update only the bounds of an existing entity.
        /// Prefer this over Remove+Insert when the entity is known to exist.
        /// </summary>
        void Update(int entityId, in AABB bounds);

        /// <summary>
        /// Query all entities whose bounds overlap the given AABB
        /// and match the layer mask.
        /// Results are appended to the provided list (list is NOT cleared).
        /// </summary>
        void Query(in AABB area, SpatialLayer layerMask, List<int> results);

        /// <summary>
        /// Convenience: query by center + radius (XZ circle, Y ignored or full height).
        /// </summary>
        void QueryRadius(Vector3 center, float radius, SpatialLayer layerMask, List<int> results);

        /// <summary>Current number of entities stored.</summary>
        int Count { get; }

        /// <summary>Optional debug: get all entity ids (for editor visualization).</summary>
        void GetAll(List<int> results);
    }
}