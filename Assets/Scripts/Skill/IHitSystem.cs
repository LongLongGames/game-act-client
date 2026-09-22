using System.Collections.Generic;
using UnityEngine;
using GameAct.Spatial;

namespace GameAct.Skill
{
    /// <summary>
    /// Hit detection service used by all skills.
    /// Depends only on ISpatialIndex + LogicCollider data.
    /// </summary>
    public interface IHitSystem
    {
        /// <summary>
        /// Instant sphere / box overlap query.
        /// </summary>
        void OverlapSphere(Vector3 center, float radius, SpatialLayer layerMask, List<HitResult> results, int maxTargets = 0);

        /// <summary>
        /// Instant box overlap (axis-aligned for simplicity).
        /// </summary>
        void OverlapBox(Vector3 center, Vector3 halfExtents, SpatialLayer layerMask, List<HitResult> results, int maxTargets = 0);

        /// <summary>
        /// Hitscan / ray query. Returns closest or all along the ray.
        /// </summary>
        void Raycast(Vector3 origin, Vector3 direction, float maxDistance, SpatialLayer layerMask, List<HitResult> results, bool closestOnly = true);

        /// <summary>
        /// Fan / sector query (angle in degrees, on XZ plane mainly).
        /// </summary>
        void QueryFan(Vector3 origin, Vector3 forward, float angle, float radius, SpatialLayer layerMask, List<HitResult> results, int maxTargets = 0);
    }
}