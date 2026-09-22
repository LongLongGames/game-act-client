using System.Collections.Generic;
using UnityEngine;

namespace GameAct.Spatial
{
    /// <summary>
    /// Basic Spatial Hash implementation of ISpatialIndex.
    /// 
    /// Design goals:
    /// - Simple, readable, zero external dependencies
    /// - Easy to replace later with Quadtree / Burst / C++ 
    /// - Supports layers for Ground / Aerial separation
    /// - Handles dynamic insert / update / remove
    /// 
    /// Not production-optimized (uses Dictionary + List).
    /// Good enough for prototype and medium entity counts (hundreds ~ low thousands).
    /// </summary>
    public class SpatialHash : ISpatialIndex
    {
        private readonly float _cellSize;
        private readonly float _invCellSize;

        // cell key → list of entity ids in that cell
        private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>(256);

        // entityId → current entry (for fast update/remove)
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>(128);

        private struct Entry
        {
            public AABB Bounds;
            public SpatialLayer Layer;
            public int CellMinX, CellMinY, CellMinZ;
            public int CellMaxX, CellMaxY, CellMaxZ;
        }

        public int Count => _entries.Count;

        public SpatialHash(float cellSize = 10f)
        {
            _cellSize = Mathf.Max(0.1f, cellSize);
            _invCellSize = 1f / _cellSize;
        }

        public void Clear()
        {
            _cells.Clear();
            _entries.Clear();
        }

        public void Insert(int entityId, in AABB bounds, SpatialLayer layer = SpatialLayer.Ground)
        {
            if (_entries.ContainsKey(entityId))
            {
                Update(entityId, bounds); // treat as update, keep old layer? or overwrite
                // For simplicity we overwrite layer too
                var e = _entries[entityId];
                e.Layer = layer;
                _entries[entityId] = e;
                return;
            }

            var entry = new Entry
            {
                Bounds = bounds,
                Layer = layer
            };
            ComputeCellRange(bounds, ref entry);
            _entries[entityId] = entry;
            AddToCells(entityId, entry);
        }

        public void Remove(int entityId)
        {
            if (!_entries.TryGetValue(entityId, out var entry))
                return;

            RemoveFromCells(entityId, entry);
            _entries.Remove(entityId);
        }

        public void Update(int entityId, in AABB bounds)
        {
            if (!_entries.TryGetValue(entityId, out var oldEntry))
            {
                // Not present → insert with default layer
                Insert(entityId, bounds, SpatialLayer.Ground);
                return;
            }

            // Fast path: if still in the same cell range, just update bounds
            int newMinX = Floor(bounds.Min.x);
            int newMinY = Floor(bounds.Min.y);
            int newMinZ = Floor(bounds.Min.z);
            int newMaxX = Floor(bounds.Max.x);
            int newMaxY = Floor(bounds.Max.y);
            int newMaxZ = Floor(bounds.Max.z);

            if (newMinX == oldEntry.CellMinX && newMinY == oldEntry.CellMinY && newMinZ == oldEntry.CellMinZ &&
                newMaxX == oldEntry.CellMaxX && newMaxY == oldEntry.CellMaxY && newMaxZ == oldEntry.CellMaxZ)
            {
                oldEntry.Bounds = bounds;
                _entries[entityId] = oldEntry;
                return;
            }

            // Cell range changed → remove from old cells, add to new
            RemoveFromCells(entityId, oldEntry);
            oldEntry.Bounds = bounds;
            oldEntry.CellMinX = newMinX;
            oldEntry.CellMinY = newMinY;
            oldEntry.CellMinZ = newMinZ;
            oldEntry.CellMaxX = newMaxX;
            oldEntry.CellMaxY = newMaxY;
            oldEntry.CellMaxZ = newMaxZ;
            _entries[entityId] = oldEntry;
            AddToCells(entityId, oldEntry);
        }

        public void Query(in AABB area, SpatialLayer layerMask, List<int> results)
        {
            if (results == null) return;

            int minX = Floor(area.Min.x);
            int minY = Floor(area.Min.y);
            int minZ = Floor(area.Min.z);
            int maxX = Floor(area.Max.x);
            int maxY = Floor(area.Max.y);
            int maxZ = Floor(area.Max.z);

            // Temporary set to avoid duplicates when entity spans multiple cells
            // For prototype we use a simple HashSet; can be replaced with faster structure later
            var temp = HashSetPool.Get();

            for (int z = minZ; z <= maxZ; z++)
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                long key = PackKey(x, y, z);
                if (!_cells.TryGetValue(key, out var list)) continue;

                for (int i = 0; i < list.Count; i++)
                {
                    int id = list[i];
                    if (!temp.Add(id)) continue; // already added

                    if (_entries.TryGetValue(id, out var entry))
                    {
                        if ((entry.Layer & layerMask) == 0) continue;
                        if (entry.Bounds.Overlaps(area))
                            results.Add(id);
                    }
                }
            }

            HashSetPool.Release(temp);
        }

        public void QueryRadius(Vector3 center, float radius, SpatialLayer layerMask, List<int> results)
        {
            // Approximate with AABB first, then optional precise filter can be added later
            float r = radius;
            var area = new AABB(
                new Vector3(center.x - r, center.y - r, center.z - r),
                new Vector3(center.x + r, center.y + r, center.z + r)
            );
            Query(area, layerMask, results);

            // Optional: precise sphere filter (commented for performance in prototype)
            // for (int i = results.Count - 1; i >= 0; i--)
            // {
            //     if (_entries.TryGetValue(results[i], out var e))
            //     {
            //         if (e.Bounds.DistanceSqr(center) > radius * radius)
            //             results.RemoveAt(i);
            //     }
            // }
        }

        public void GetAll(List<int> results)
        {
            if (results == null) return;
            foreach (var id in _entries.Keys)
                results.Add(id);
        }

        // --------- Internal helpers ---------

        private void ComputeCellRange(in AABB bounds, ref Entry entry)
        {
            entry.CellMinX = Floor(bounds.Min.x);
            entry.CellMinY = Floor(bounds.Min.y);
            entry.CellMinZ = Floor(bounds.Min.z);
            entry.CellMaxX = Floor(bounds.Max.x);
            entry.CellMaxY = Floor(bounds.Max.y);
            entry.CellMaxZ = Floor(bounds.Max.z);
        }

        private void AddToCells(int entityId, in Entry entry)
        {
            for (int z = entry.CellMinZ; z <= entry.CellMaxZ; z++)
            for (int y = entry.CellMinY; y <= entry.CellMaxY; y++)
            for (int x = entry.CellMinX; x <= entry.CellMaxX; x++)
            {
                long key = PackKey(x, y, z);
                if (!_cells.TryGetValue(key, out var list))
                {
                    list = ListPool.Get();
                    _cells[key] = list;
                }
                list.Add(entityId);
            }
        }

        private void RemoveFromCells(int entityId, in Entry entry)
        {
            for (int z = entry.CellMinZ; z <= entry.CellMaxZ; z++)
            for (int y = entry.CellMinY; y <= entry.CellMaxY; y++)
            for (int x = entry.CellMinX; x <= entry.CellMaxX; x++)
            {
                long key = PackKey(x, y, z);
                if (_cells.TryGetValue(key, out var list))
                {
                    list.Remove(entityId);
                    if (list.Count == 0)
                    {
                        _cells.Remove(key);
                        ListPool.Release(list);
                    }
                }
            }
        }

        private int Floor(float v) => Mathf.FloorToInt(v * _invCellSize);

        // Pack 3D cell coords into a single long key
        // Assumes reasonable world size (coords roughly -50000 ~ 50000)
        private static long PackKey(int x, int y, int z)
        {
            // Offset to positive range
            const int offset = 50000;
            long lx = (long)(x + offset);
            long ly = (long)(y + offset);
            long lz = (long)(z + offset);
            return (lx) | (ly << 20) | (lz << 40);
        }

        // --------- Simple non-alloc pools (prototype level) ---------

        private static class ListPool
        {
            private static readonly Stack<List<int>> Pool = new Stack<List<int>>(32);

            public static List<int> Get()
            {
                return Pool.Count > 0 ? Pool.Pop() : new List<int>(8);
            }

            public static void Release(List<int> list)
            {
                list.Clear();
                Pool.Push(list);
            }
        }

        private static class HashSetPool
        {
            private static readonly Stack<HashSet<int>> Pool = new Stack<HashSet<int>>(8);

            public static HashSet<int> Get()
            {
                return Pool.Count > 0 ? Pool.Pop() : new HashSet<int>();
            }

            public static void Release(HashSet<int> set)
            {
                set.Clear();
                Pool.Push(set);
            }
        }
    }
}