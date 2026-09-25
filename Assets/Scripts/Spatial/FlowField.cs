using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameAct.Spatial
{
    /// <summary>
    /// 简易 Flow Field（与 SpatialHash 格子可对齐）。
    /// 用于群体追击：目标移动时重建 Integration + Flow 向量。
    /// 设计目标：原型可用、零外部依赖、后续可换 Burst/Native。
    /// </summary>
    public sealed class FlowField
    {
        public const float DefaultCellSize = 1.5f;
        public const int DefaultMaxDim = 96;

        readonly float _cellSize;
        readonly float _invCell;
        readonly int _width;
        readonly int _height;
        readonly Vector3 _origin; // 格子 (0,0) 对应的世界 XZ 角点（Y 忽略）

        // cost: 0 = 可走, >= BlockCost = 不可走
        readonly byte[] _cost;
        // integration: 到目标的代价，Unreachable 表示不可达
        readonly float[] _integration;
        // flow: 归一化 XZ 方向（Y=0）
        readonly Vector2[] _flow;

        Vector3 _lastTarget;
        bool _hasField;
        int _blockedLayerMask;

        public float CellSize => _cellSize;
        public int Width => _width;
        public int Height => _height;
        public Vector3 Origin => _origin;
        public bool HasField => _hasField;
        public Vector3 LastTarget => _lastTarget;

        public const byte WalkCost = 1;
        public const byte BlockCost = 255;
        public const float Unreachable = 1e9f;

        static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] Dz = { 0, 0, 1, -1, 1, -1, 1, -1 };
        static readonly float[] Dist = { 1f, 1f, 1f, 1f, 1.41421356f, 1.41421356f, 1.41421356f, 1.41421356f };

        public FlowField(Vector3 worldCenter, float halfExtent, float cellSize = DefaultCellSize, int maxDim = DefaultMaxDim)
        {
            _cellSize = Mathf.Max(0.25f, cellSize);
            _invCell = 1f / _cellSize;

            int dim = Mathf.Clamp(Mathf.CeilToInt((halfExtent * 2f) / _cellSize), 8, maxDim);
            // 强制奇数便于中心对齐
            if ((dim & 1) == 0) dim++;
            _width = dim;
            _height = dim;

            float total = dim * _cellSize;
            _origin = new Vector3(
                worldCenter.x - total * 0.5f,
                0f,
                worldCenter.z - total * 0.5f);

            int n = _width * _height;
            _cost = new byte[n];
            _integration = new float[n];
            _flow = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                _cost[i] = WalkCost;
                _integration[i] = Unreachable;
            }
        }

        public void SetBlockedLayerMask(int layerMask)
        {
            _blockedLayerMask = layerMask;
        }

        /// <summary>
        /// 用 Physics 在胸口高度采样障碍，写入 cost。
        /// 可在场景加载或障碍变化时调用一次。
        /// </summary>
        public void BakeObstacles(float probeRadius = 0.4f, float chestHeight = 0.9f)
        {
            int mask = _blockedLayerMask != 0 ? _blockedLayerMask : Physics.DefaultRaycastLayers;
            for (int z = 0; z < _height; z++)
            {
                for (int x = 0; x < _width; x++)
                {
                    Vector3 c = CellCenter(x, z);
                    Vector3 origin = new Vector3(c.x, chestHeight, c.z);
                    bool blocked = Physics.CheckSphere(origin, probeRadius, mask, QueryTriggerInteraction.Ignore);
                    _cost[Index(x, z)] = blocked ? BlockCost : WalkCost;
                }
            }
            _hasField = false;
        }

        /// <summary>
        /// 手动标记矩形区域为阻挡（世界 XZ）。
        /// </summary>
        public void MarkBlockedRect(float minX, float minZ, float maxX, float maxZ)
        {
            WorldToCell(new Vector3(minX, 0f, minZ), out int x0, out int z0);
            WorldToCell(new Vector3(maxX, 0f, maxZ), out int x1, out int z1);
            if (x0 > x1) { int t = x0; x0 = x1; x1 = t; }
            if (z0 > z1) { int t = z0; z0 = z1; z1 = t; }
            x0 = Mathf.Clamp(x0, 0, _width - 1);
            x1 = Mathf.Clamp(x1, 0, _width - 1);
            z0 = Mathf.Clamp(z0, 0, _height - 1);
            z1 = Mathf.Clamp(z1, 0, _height - 1);
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                    _cost[Index(x, z)] = BlockCost;
            _hasField = false;
        }

        /// <summary>
        /// 以目标世界坐标重建 Integration Field + Flow Field（Dijkstra 近似）。
        /// </summary>
        public void RebuildToward(Vector3 targetWorld)
        {
            _lastTarget = targetWorld;
            WorldToCell(targetWorld, out int tx, out int tz);
            if (tx < 0 || tx >= _width || tz < 0 || tz >= _height)
            {
                _hasField = false;
                return;
            }

            int n = _width * _height;
            for (int i = 0; i < n; i++)
                _integration[i] = Unreachable;

            // 目标格若被挡，尝试邻格
            int goal = Index(tx, tz);
            if (_cost[goal] >= BlockCost)
            {
                bool found = false;
                for (int d = 0; d < 8; d++)
                {
                    int nx = tx + Dx[d];
                    int nz = tz + Dz[d];
                    if (nx < 0 || nx >= _width || nz < 0 || nz >= _height) continue;
                    int ni = Index(nx, nz);
                    if (_cost[ni] < BlockCost)
                    {
                        tx = nx; tz = nz; goal = ni;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    _hasField = false;
                    return;
                }
            }

            _integration[goal] = 0f;

            // 简易优先队列：用 List 冒泡式（原型规模足够）
            var open = new List<int>(n / 4) { goal };
            while (open.Count > 0)
            {
                // 取 integration 最小
                int bestIdx = 0;
                float bestVal = _integration[open[0]];
                for (int i = 1; i < open.Count; i++)
                {
                    float v = _integration[open[i]];
                    if (v < bestVal) { bestVal = v; bestIdx = i; }
                }
                int cur = open[bestIdx];
                open.RemoveAt(bestIdx);

                CellOf(cur, out int cx, out int cz);
                float curCost = _integration[cur];

                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + Dx[d];
                    int nz = cz + Dz[d];
                    if (nx < 0 || nx >= _width || nz < 0 || nz >= _height) continue;
                    int ni = Index(nx, nz);
                    if (_cost[ni] >= BlockCost) continue;

                    float step = Dist[d] * _cost[ni];
                    float nd = curCost + step;
                    if (nd + 0.001f < _integration[ni])
                    {
                        _integration[ni] = nd;
                        open.Add(ni);
                    }
                }
            }

            // Flow：指向 integration 更小的邻格
            for (int z = 0; z < _height; z++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int i = Index(x, z);
                    if (_cost[i] >= BlockCost || _integration[i] >= Unreachable * 0.5f)
                    {
                        _flow[i] = Vector2.zero;
                        continue;
                    }

                    float best = _integration[i];
                    float bx = 0f, bz = 0f;
                    for (int d = 0; d < 8; d++)
                    {
                        int nx = x + Dx[d];
                        int nz = z + Dz[d];
                        if (nx < 0 || nx >= _width || nz < 0 || nz >= _height) continue;
                        int ni = Index(nx, nz);
                        float v = _integration[ni];
                        if (v < best)
                        {
                            best = v;
                            bx = Dx[d];
                            bz = Dz[d];
                        }
                    }
                    if (bx != 0f || bz != 0f)
                    {
                        float len = Mathf.Sqrt(bx * bx + bz * bz);
                        _flow[i] = new Vector2(bx / len, bz / len);
                    }
                    else
                        _flow[i] = Vector2.zero;
                }
            }

            _hasField = true;
        }

        /// <summary>
        /// 采样世界坐标处的流动方向（XZ，Y=0）。无场或不可达时返回 zero。
        /// </summary>
        public Vector3 SampleDirection(Vector3 worldPos)
        {
            if (!_hasField) return Vector3.zero;
            WorldToCell(worldPos, out int x, out int z);
            if (x < 0 || x >= _width || z < 0 || z >= _height) return Vector3.zero;
            var f = _flow[Index(x, z)];
            if (f.sqrMagnitude < 1e-6f) return Vector3.zero;
            return new Vector3(f.x, 0f, f.y);
        }

        /// <summary>按格子索引读 Flow + Integration（调试箭头阵列用）。</summary>
        public bool TryGetCell(int cx, int cz, out Vector2 flowXZ, out float integration, out bool blocked)
        {
            flowXZ = Vector2.zero;
            integration = Unreachable;
            blocked = true;
            if (!_hasField || cx < 0 || cx >= _width || cz < 0 || cz >= _height)
                return false;
            int i = Index(cx, cz);
            blocked = _cost[i] >= BlockCost;
            integration = _integration[i];
            flowXZ = _flow[i];
            return true;
        }

        /// <summary>
        /// 到目标的 integration 代价（越大越远，Unreachable 不可达）。
        /// </summary>
        public float SampleCost(Vector3 worldPos)
        {
            if (!_hasField) return Unreachable;
            WorldToCell(worldPos, out int x, out int z);
            if (x < 0 || x >= _width || z < 0 || z >= _height) return Unreachable;
            return _integration[Index(x, z)];
        }

        public bool WorldToCell(Vector3 world, out int cx, out int cz)
        {
            cx = Mathf.FloorToInt((world.x - _origin.x) * _invCell);
            cz = Mathf.FloorToInt((world.z - _origin.z) * _invCell);
            return cx >= 0 && cx < _width && cz >= 0 && cz < _height;
        }

        public Vector3 CellCenter(int cx, int cz)
        {
            return new Vector3(
                _origin.x + (cx + 0.5f) * _cellSize,
                0f,
                _origin.z + (cz + 0.5f) * _cellSize);
        }

        int Index(int x, int z) => z * _width + x;

        void CellOf(int index, out int x, out int z)
        {
            z = index / _width;
            x = index - z * _width;
        }
    }

    /// <summary>
    /// 全局 Flow Field 服务：权威侧按最近玩家目标周期性重建。
    /// 怪物 AI 直接 SampleDirection。
    /// </summary>
    public static class FlowFieldService
    {
        static FlowField _field;
        static float _rebuildTimer;
        static Vector3 _lastRebuildTarget;
        const float RebuildInterval = 0.35f;
        const float RebuildMoveThreshold = 1.2f;

        public static FlowField Field => _field;
        public static bool IsReady => _field != null && _field.HasField;

        /// <summary>
        /// 在场景中心初始化。建议在刷怪/开局时调用一次。
        /// </summary>
        public static void Ensure(Vector3 worldCenter, float halfExtent = 48f, float cellSize = FlowField.DefaultCellSize)
        {
            if (_field != null) return;
            _field = new FlowField(worldCenter, halfExtent, cellSize);
            _field.BakeObstacles();
            _rebuildTimer = 0f;
        }

        public static void Reset()
        {
            _field = null;
            _rebuildTimer = 0f;
            _lastRebuildTarget = Vector3.zero;
        }

        /// <summary>
        /// 每帧/权威 Tick 调用：若目标移动足够远或超时则重建。
        /// </summary>
        public static void Tick(Vector3 targetWorld, float dt)
        {
            if (_field == null) return;

            _rebuildTimer -= dt;
            float moved = (targetWorld - _lastRebuildTarget).sqrMagnitude;
            if (_rebuildTimer > 0f && moved < RebuildMoveThreshold * RebuildMoveThreshold && _field.HasField)
                return;

            _field.RebuildToward(targetWorld);
            _lastRebuildTarget = targetWorld;
            _rebuildTimer = RebuildInterval;
        }

        public static Vector3 SampleDirection(Vector3 worldPos)
        {
            return _field != null ? _field.SampleDirection(worldPos) : Vector3.zero;
        }
    }
}
