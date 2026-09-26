using System.Collections.Generic;
using UnityEngine;

namespace GameAct.Spatial
{
    /// <summary>
    /// One observer (usually a player / camera focus).
    /// Holds the last known tier of every entity relative to this observer.
    /// Host/Server uses observers to filter what each client receives.
    /// </summary>
    public class AoiObserver
    {
        public int ObserverId;
        public Vector3 Position;
        public AoiConfig Config;

        // entityId → current tier for this observer
        private readonly Dictionary<int, AoiTier> _tiers = new Dictionary<int, AoiTier>(128);

        // Reusable query buffers
        private readonly List<int> _queryBuf = new List<int>(128);
        private readonly HashSet<int> _seenThisFrame = new HashSet<int>();

        public AoiObserver(int observerId, in AoiConfig config)
        {
            ObserverId = observerId;
            Config = config;
            Position = Vector3.zero;
        }

        public void SetPosition(Vector3 pos) => Position = pos;

        public AoiTier GetTier(int entityId)
        {
            return _tiers.TryGetValue(entityId, out var t) ? t : AoiTier.None;
        }

        public IReadOnlyDictionary<int, AoiTier> Tiers => _tiers;

        /// <summary>
        /// Rebuild tiers from the spatial index around the current position.
        /// Call once per tick (or every N ticks) per observer.
        /// </summary>
        public void Refresh(ISpatialIndex spatial)
        {
            if (spatial == null) return;

            _queryBuf.Clear();
            _seenThisFrame.Clear();

            float maxR = Config.MaxRadius;
            spatial.QueryRadius(Position, maxR, Config.QueryLayerMask, _queryBuf);

            for (int i = 0; i < _queryBuf.Count; i++)
            {
                int id = _queryBuf[i];
                if (id == ObserverId) continue; // never track self as interest target

                _seenThisFrame.Add(id);

                // We need approximate distance; spatial only gave us candidates.
                // Callers should keep a position lookup; for now we re-query bounds
                // via a temporary AABB distance (caller can inject a position provider later).
                // Tier is refined externally if better distance is available.
                AoiTier prev = GetTier(id);
                AoiTier next = prev == AoiTier.None ? AoiTier.Far : prev; // provisional
                _tiers[id] = next;
            }

            // Drop entities that left the far radius
            var toRemove = ListPool.Get();
            foreach (var kv in _tiers)
            {
                if (!_seenThisFrame.Contains(kv.Key))
                    toRemove.Add(kv.Key);
            }
            for (int i = 0; i < toRemove.Count; i++)
                _tiers.Remove(toRemove[i]);
            ListPool.Release(toRemove);
        }

        /// <summary>
        /// Precise tier evaluation given world positions (preferred path).
        /// </summary>
        public AoiTier EvaluateTier(Vector3 entityPos, AoiTier previous)
        {
            float dx = entityPos.x - Position.x;
            float dz = entityPos.z - Position.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);

            // Enter thresholds (tighter)
            float nearEnter = Config.NearRadius - Config.EnterHysteresis;
            float midEnter  = Config.MidRadius  - Config.EnterHysteresis;
            float farEnter  = Config.FarRadius  - Config.EnterHysteresis;

            // Leave thresholds (looser)
            float nearLeave = Config.NearRadius + Config.LeaveHysteresis;
            float midLeave  = Config.MidRadius  + Config.LeaveHysteresis;
            float farLeave  = Config.FarRadius  + Config.LeaveHysteresis;

            switch (previous)
            {
                case AoiTier.Near:
                    if (dist <= nearLeave) return AoiTier.Near;
                    if (dist <= midLeave)  return AoiTier.Mid;
                    if (dist <= farLeave)  return AoiTier.Far;
                    return AoiTier.None;

                case AoiTier.Mid:
                    if (dist <= nearEnter) return AoiTier.Near;
                    if (dist <= midLeave)  return AoiTier.Mid;
                    if (dist <= farLeave)  return AoiTier.Far;
                    return AoiTier.None;

                case AoiTier.Far:
                    if (dist <= nearEnter) return AoiTier.Near;
                    if (dist <= midEnter)  return AoiTier.Mid;
                    if (dist <= farLeave)  return AoiTier.Far;
                    return AoiTier.None;

                default: // None or Swarm
                    if (dist <= nearEnter) return AoiTier.Near;
                    if (dist <= midEnter)  return AoiTier.Mid;
                    if (dist <= farEnter)  return AoiTier.Far;
                    return AoiTier.None;
            }
        }

        public void SetTier(int entityId, AoiTier tier)
        {
            if (tier == AoiTier.None)
                _tiers.Remove(entityId);
            else
                _tiers[entityId] = tier;
        }

        public void Clear()
        {
            _tiers.Clear();
            _queryBuf.Clear();
            _seenThisFrame.Clear();
        }

        // Simple non-alloc list pool (local to this file)
        private static class ListPool
        {
            private static readonly Stack<List<int>> Pool = new Stack<List<int>>(4);
            public static List<int> Get() => Pool.Count > 0 ? Pool.Pop() : new List<int>(32);
            public static void Release(List<int> list)
            {
                list.Clear();
                Pool.Push(list);
            }
        }
    }
}
