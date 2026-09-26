using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameAct.Spatial
{
    /// <summary>
    /// Central AOI manager.
    /// 
    /// Responsibilities:
    /// - Maintain observers (players)
    /// - Maintain entity positions (lightweight cache)
    /// - Each tick: for every observer, classify entities into Near / Mid / Far
    /// - Expose per-observer interest sets for network filtering
    /// - Mark Swarm-tier entities for pure client-side group simulation
    /// 
    /// Does NOT own the spatial index; receives ISpatialIndex from outside
    /// (usually SectionManager.ActiveSpatial).
    /// </summary>
    public class AoiSystem
    {
        public AoiConfig Config { get; private set; }

        private ISpatialIndex _spatial;
        private readonly Dictionary<int, AoiObserver> _observers = new Dictionary<int, AoiObserver>(8);
        private readonly Dictionary<int, Vector3> _entityPositions = new Dictionary<int, Vector3>(256);
        private readonly HashSet<int> _swarmEntities = new HashSet<int>(); // pure client-side

        // Events for higher layers (net filter, view culling, etc.)
        public event Action<int, int, AoiTier, AoiTier> OnTierChanged; // observerId, entityId, old, new

        public AoiSystem(AoiConfig? config = null)
        {
            Config = config ?? AoiConfig.Default;
        }

        public void SetSpatial(ISpatialIndex spatial) => _spatial = spatial;

        public void SetConfig(in AoiConfig config) => Config = config;

        // ---------- Observer management ----------

        public AoiObserver AddObserver(int observerId, Vector3 position)
        {
            if (_observers.TryGetValue(observerId, out var existing))
            {
                existing.SetPosition(position);
                return existing;
            }

            var obs = new AoiObserver(observerId, Config);
            obs.SetPosition(position);
            _observers[observerId] = obs;
            return obs;
        }

        public void RemoveObserver(int observerId)
        {
            if (_observers.TryGetValue(observerId, out var obs))
            {
                obs.Clear();
                _observers.Remove(observerId);
            }
        }

        public bool TryGetObserver(int observerId, out AoiObserver observer)
            => _observers.TryGetValue(observerId, out observer);

        public IReadOnlyDictionary<int, AoiObserver> Observers => _observers;

        // ---------- Entity position cache ----------

        public void UpdateEntityPosition(int entityId, Vector3 position)
        {
            _entityPositions[entityId] = position;
        }

        public void RemoveEntity(int entityId)
        {
            _entityPositions.Remove(entityId);
            _swarmEntities.Remove(entityId);

            foreach (var kv in _observers)
                kv.Value.SetTier(entityId, AoiTier.None);
        }

        public bool TryGetEntityPosition(int entityId, out Vector3 pos)
            => _entityPositions.TryGetValue(entityId, out pos);

        // ---------- Swarm (client-only group) ----------

        public void MarkAsSwarm(int entityId) => _swarmEntities.Add(entityId);
        public void UnmarkSwarm(int entityId) => _swarmEntities.Remove(entityId);
        public bool IsSwarm(int entityId) => _swarmEntities.Contains(entityId);

        // ---------- Core tick ----------

        /// <summary>
        /// Call once per simulation tick (or every N ticks for cheaper updates).
        /// Updates all observers' interest sets.
        /// </summary>
        public void Tick()
        {
            if (_spatial == null) return;

            foreach (var kv in _observers)
            {
                var obs = kv.Value;
                RefreshObserver(obs);
            }
        }

        private void RefreshObserver(AoiObserver obs)
        {
            // 1) Broad-phase candidates via spatial
            var candidates = CandidateBuffer.Get();
            candidates.Clear();
            _spatial.QueryRadius(obs.Position, obs.Config.MaxRadius, obs.Config.QueryLayerMask, candidates);

            var stillVisible = SeenBuffer.Get();
            stillVisible.Clear();

            for (int i = 0; i < candidates.Count; i++)
            {
                int entityId = candidates[i];
                if (entityId == obs.ObserverId) continue;

                if (!_entityPositions.TryGetValue(entityId, out var epos))
                    continue;

                AoiTier prev = obs.GetTier(entityId);
                AoiTier next;

                if (_swarmEntities.Contains(entityId))
                {
                    // Swarm entities never enter precise tiers for network;
                    // they stay Swarm (client-side only).
                    next = AoiTier.Swarm;
                }
                else
                {
                    next = obs.EvaluateTier(epos, prev);
                }

                if (next != AoiTier.None)
                    stillVisible.Add(entityId);

                if (next != prev)
                {
                    obs.SetTier(entityId, next);
                    OnTierChanged?.Invoke(obs.ObserverId, entityId, prev, next);
                }
            }

            // 2) Remove entities that left Far radius
            var stale = StaleBuffer.Get();
            stale.Clear();
            foreach (var kv in obs.Tiers)
            {
                if (!stillVisible.Contains(kv.Key))
                    stale.Add(kv.Key);
            }
            for (int i = 0; i < stale.Count; i++)
            {
                int id = stale[i];
                AoiTier prev = obs.GetTier(id);
                obs.SetTier(id, AoiTier.None);
                if (prev != AoiTier.None)
                    OnTierChanged?.Invoke(obs.ObserverId, id, prev, AoiTier.None);
            }

            CandidateBuffer.Release(candidates);
            SeenBuffer.Release(stillVisible);
            StaleBuffer.Release(stale);
        }

        /// <summary>
        /// Convenience: collect all entity ids currently in a given tier for an observer.
        /// Results are appended (list is NOT cleared).
        /// </summary>
        public void GetEntitiesInTier(int observerId, AoiTier tier, List<int> results)
        {
            if (results == null) return;
            if (!_observers.TryGetValue(observerId, out var obs)) return;

            foreach (var kv in obs.Tiers)
            {
                if (kv.Value == tier)
                    results.Add(kv.Key);
            }
        }

        /// <summary>
        /// True if the entity should be network-synced to this observer
        /// (Near or Mid). Far/Swarm/None are not sent or sent sparsely.
        /// </summary>
        public bool ShouldSync(int observerId, int entityId)
        {
            if (!_observers.TryGetValue(observerId, out var obs)) return false;
            var t = obs.GetTier(entityId);
            return t == AoiTier.Near || t == AoiTier.Mid;
        }

        public void Clear()
        {
            foreach (var kv in _observers)
                kv.Value.Clear();
            _observers.Clear();
            _entityPositions.Clear();
            _swarmEntities.Clear();
        }

        // ---------- lightweight buffers ----------

        private static class CandidateBuffer
        {
            private static readonly Stack<List<int>> Pool = new Stack<List<int>>(4);
            public static List<int> Get() => Pool.Count > 0 ? Pool.Pop() : new List<int>(128);
            public static void Release(List<int> l) { l.Clear(); Pool.Push(l); }
        }

        private static class SeenBuffer
        {
            private static readonly Stack<HashSet<int>> Pool = new Stack<HashSet<int>>(4);
            public static HashSet<int> Get() => Pool.Count > 0 ? Pool.Pop() : new HashSet<int>();
            public static void Release(HashSet<int> s) { s.Clear(); Pool.Push(s); }
        }

        private static class StaleBuffer
        {
            private static readonly Stack<List<int>> Pool = new Stack<List<int>>(4);
            public static List<int> Get() => Pool.Count > 0 ? Pool.Pop() : new List<int>(32);
            public static void Release(List<int> l) { l.Clear(); Pool.Push(l); }
        }
    }
}
