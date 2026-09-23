using System.Collections.Generic;
using UnityEngine;
using GameAct.Spatial;

namespace GameAct.Skill
{
    /// <summary>
    /// 统一可命中目标注册表。LES MonsterView / Debug 刷怪都走这里，技能只查此表。
    /// </summary>
    public static class CombatTargetRegistry
    {
        public struct Entry
        {
            public int EntityId;
            public HitReceiver Receiver;
            public LogicCollider Collider;
            public Transform Transform;
        }

        static readonly Dictionary<int, Entry> _map = new Dictionary<int, Entry>(64);
        static int _nextDebugId = 100000;

        public static IReadOnlyDictionary<int, Entry> All => _map;

        public static int AllocDebugEntityId() => _nextDebugId++;

        public static void Register(int entityId, HitReceiver receiver, LogicCollider collider = null)
        {
            if (receiver == null) return;
            if (collider == null)
                collider = receiver.GetComponent<LogicCollider>();

            receiver.EntityId = entityId;
            _map[entityId] = new Entry
            {
                EntityId = entityId,
                Receiver = receiver,
                Collider = collider,
                Transform = receiver.transform
            };
        }

        public static void Unregister(int entityId)
        {
            _map.Remove(entityId);
        }

        public static void Unregister(HitReceiver receiver)
        {
            if (receiver == null) return;
            Unregister(receiver.EntityId);
        }

        public static bool TryGet(int entityId, out Entry entry) => _map.TryGetValue(entityId, out entry);

        public static bool TryGetReceiver(int entityId, out HitReceiver receiver)
        {
            if (_map.TryGetValue(entityId, out var e) && e.Receiver != null)
            {
                receiver = e.Receiver;
                return true;
            }
            receiver = null;
            return false;
        }

        public static AABB? GetBounds(int entityId)
        {
            if (!_map.TryGetValue(entityId, out var e) || e.Receiver == null || !e.Receiver.gameObject.activeInHierarchy)
                return null;

            if (e.Collider != null && e.Collider.IsInitialized)
                return e.Collider.GetWorldBounds();

            var t = e.Transform;
            return AABB.FromCenterSize(t.position + Vector3.up, new Vector3(0.8f, 2f, 0.8f));
        }

        /// <summary>把当前全部目标写入 Spatial（每帧或施法前调用）。</summary>
        public static void SyncToSpatial(ISpatialIndex spatial, SpatialLayer layer = SpatialLayer.Ground)
        {
            if (spatial == null) return;
            spatial.Clear();
            foreach (var kv in _map)
            {
                var e = kv.Value;
                if (e.Receiver == null || !e.Receiver.gameObject.activeInHierarchy) continue;
                var b = GetBounds(kv.Key);
                if (b == null) continue;
                spatial.Insert(kv.Key, b.Value, layer);
            }
        }

        public static void Clear() => _map.Clear();

        public static int CountAlive()
        {
            int n = 0;
            foreach (var kv in _map)
            {
                if (kv.Value.Receiver != null && kv.Value.Receiver.gameObject.activeInHierarchy)
                    n++;
            }
            return n;
        }
    }
}
