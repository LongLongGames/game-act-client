using System.Collections.Generic;
using UnityEngine;
using GameAct.Spatial;

namespace GameAct.Skill
{
    /// <summary>
    /// Default HitSystem implementation using ISpatialIndex.
    /// 
    /// Broadphase: Spatial query
    /// Narrowphase: simple distance / ray-AABB tests (can be upgraded later)
    /// </summary>
    public class HitSystem : IHitSystem
    {
        private readonly ISpatialIndex _spatial;
        private readonly List<int> _tempIds = new List<int>(32);

        // Optional: external provider that can resolve entityId → world AABB / position
        // For prototype we keep it simple; real project should inject a lookup.
        public System.Func<int, AABB?> GetEntityBounds;

        public HitSystem(ISpatialIndex spatial)
        {
            _spatial = spatial;
        }

        public void OverlapSphere(Vector3 center, float radius, SpatialLayer layerMask, List<HitResult> results, int maxTargets = 0)
        {
            results.Clear();
            _tempIds.Clear();

            _spatial.QueryRadius(center, radius, layerMask, _tempIds);

            float rSqr = radius * radius;
            int count = 0;

            foreach (int id in _tempIds)
            {
                if (maxTargets > 0 && count >= maxTargets) break;

                if (GetEntityBounds != null)
                {
                    var b = GetEntityBounds(id);
                    if (b == null) continue;
                    if (b.Value.DistanceSqr(center) > rSqr) continue;

                    results.Add(new HitResult
                    {
                        TargetEntityId = id,
                        HitPoint = b.Value.Center,
                        Distance = Vector3.Distance(center, b.Value.Center)
                    });
                }
                else
                {
                    // Fallback: trust broadphase only
                    results.Add(new HitResult { TargetEntityId = id, HitPoint = center });
                }
                count++;
            }
        }

        public void OverlapBox(Vector3 center, Vector3 halfExtents, SpatialLayer layerMask, List<HitResult> results, int maxTargets = 0)
        {
            results.Clear();
            _tempIds.Clear();

            var area = AABB.FromCenterExtents(center, halfExtents);
            _spatial.Query(area, layerMask, _tempIds);

            int count = 0;
            foreach (int id in _tempIds)
            {
                if (maxTargets > 0 && count >= maxTargets) break;

                results.Add(new HitResult
                {
                    TargetEntityId = id,
                    HitPoint = center
                });
                count++;
            }
        }

        public void Raycast(Vector3 origin, Vector3 direction, float maxDistance, SpatialLayer layerMask, List<HitResult> results, bool closestOnly = true)
        {
            results.Clear();
            _tempIds.Clear();

            // Approximate ray with a thin long box / radius query for broadphase
            Vector3 end = origin + direction.normalized * maxDistance;
            Vector3 mid = (origin + end) * 0.5f;
            float dist = maxDistance * 0.5f;
            // Simple inflated AABB along the ray
            var area = new AABB(
                Vector3.Min(origin, end) - Vector3.one * 0.5f,
                Vector3.Max(origin, end) + Vector3.one * 0.5f
            );
            _spatial.Query(area, layerMask, _tempIds);

            HitResult? closest = null;
            float closestDist = float.MaxValue;

            foreach (int id in _tempIds)
            {
                if (GetEntityBounds == null)
                {
                    results.Add(new HitResult { TargetEntityId = id, HitPoint = mid });
                    if (closestOnly) break;
                    continue;
                }

                var b = GetEntityBounds(id);
                if (b == null) continue;

                // Very simple ray vs AABB test (slab method would be better later)
                Vector3 c = b.Value.Center;
                Vector3 toCenter = c - origin;
                float proj = Vector3.Dot(toCenter, direction.normalized);
                if (proj < 0f || proj > maxDistance) continue;

                float distToRay = (toCenter - direction.normalized * proj).magnitude;
                float approxRadius = b.Value.Extents.magnitude * 0.7f;
                if (distToRay > approxRadius) continue;

                var hit = new HitResult
                {
                    TargetEntityId = id,
                    HitPoint = origin + direction.normalized * proj,
                    Distance = proj
                };

                if (closestOnly)
                {
                    if (proj < closestDist)
                    {
                        closestDist = proj;
                        closest = hit;
                    }
                }
                else
                {
                    results.Add(hit);
                }
            }

            if (closestOnly && closest.HasValue)
                results.Add(closest.Value);
        }

        public void QueryFan(Vector3 origin, Vector3 forward, float angle, float radius, SpatialLayer layerMask, List<HitResult> results, int maxTargets = 0)
        {
            results.Clear();
            _tempIds.Clear();

            _spatial.QueryRadius(origin, radius, layerMask, _tempIds);

            forward.y = 0f;
            forward.Normalize();
            float halfAngle = angle * 0.5f;
            int count = 0;

            foreach (int id in _tempIds)
            {
                if (maxTargets > 0 && count >= maxTargets) break;

                Vector3 targetPos = origin;
                if (GetEntityBounds != null)
                {
                    var b = GetEntityBounds(id);
                    if (b == null) continue;
                    targetPos = b.Value.Center;
                }

                Vector3 toTarget = targetPos - origin;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;
                if (dist > radius || dist < 0.01f) continue;

                float ang = Vector3.Angle(forward, toTarget);
                if (ang > halfAngle) continue;

                results.Add(new HitResult
                {
                    TargetEntityId = id,
                    HitPoint = targetPos,
                    Distance = dist
                });
                count++;
            }
        }
    }
}