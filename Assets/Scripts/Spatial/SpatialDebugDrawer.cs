using GameAct.Gameplay.Player;
using System.Collections.Generic;
using UnityEngine;

namespace GameAct.Spatial
{
    /// <summary>
    /// Unified Gizmos / debug visualization for Spatial system.
    /// 
    /// Attach this to any GameObject (usually a Debug or Systems object).
    /// Assign the ISpatialIndex (or SectionManager) you want to visualize.
    /// 
    /// Supports:
    /// - AOI / Query range
    /// - Entity Hitboxes (bounds)
    /// - Spatial Hash cell grid (approximate)
    /// - Last query results highlight
    /// </summary>
    public class SpatialDebugDrawer : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Direct spatial index to visualize. If null, tries SectionManager.ActiveSpatial")]
        public bool useSectionManager = false;

        [Header("AOI / Query")]
        public bool drawAoi = true;
        public Transform aoiCenter;                 // usually the player
        public float aoiRadius = 20f;
        public SpatialLayer aoiLayerMask = SpatialLayer.All;
        public Color aoiColor = new Color(0f, 1f, 0.3f, 0.25f);
        public Color aoiResultColor = Color.yellow;

        [Header("Hitboxes")]
        public bool drawHitboxes = true;
        public Color hitboxColor = new Color(1f, 0.4f, 0.1f, 0.8f);
        public Color selectedHitboxColor = Color.cyan;

        [Header("Grid (SpatialHash approx)")]
        public bool drawGrid = false;
        public float gridCellSize = 10f;
        public int gridRange = 8;                   // how many cells around center to draw
        public Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);

        [Header("Runtime")]
        public bool autoQueryEveryFrame = true;

        // Runtime references (set from code)
        private ISpatialIndex _index;
        private SectionManager _sectionManager;

        private readonly List<int> _queryResults = new List<int>(64);
        private readonly Dictionary<int, AABB> _debugBounds = new Dictionary<int, AABB>();

        /// <summary>Inject the spatial index directly.</summary>
        public void SetSpatialIndex(ISpatialIndex index)
        {
            _index = index;
            useSectionManager = false;
        }

        /// <summary>Inject SectionManager (will use ActiveSpatial).</summary>
        public void SetSectionManager(SectionManager manager)
        {
            _sectionManager = manager;
            useSectionManager = true;
        }

        /// <summary>
        /// Manually register an entity bounds for hitbox drawing.
        /// Call this when you insert/update entities if you want persistent hitbox viz.
        /// </summary>
        public void SetEntityBounds(int entityId, in AABB bounds)
        {
            _debugBounds[entityId] = bounds;
        }

        public void RemoveEntityBounds(int entityId)
        {
            _debugBounds.Remove(entityId);
        }

        public void ClearEntityBounds()
        {
            _debugBounds.Clear();
        }

        private ISpatialIndex GetCurrentIndex()
        {
            if (useSectionManager && _sectionManager != null)
                return _sectionManager.ActiveSpatial;
            return _index;
        }

        // 在类里现有字段后面加
        [Header("Auto Bind")]
        [Tooltip("启动时若 aoiCenter 为空，自动找 Tag=Player")]
        public bool autoFindPlayer = true;

        // SpatialDebugDrawer 里把原来的 Awake 删掉，改成：
        private void LateUpdate()
        {
            if (aoiCenter == null)
            {
                var views = FindObjectsOfType<PlayerView>();
                foreach (var v in views)
                {
                    if (v != null && v.IsLocal)
                    {
                        aoiCenter = v.transform;
                        break;
                    }
                }
                if (aoiCenter == null)
                {
                    var go = GameObject.Find("Player_Local");
                    if (go != null) aoiCenter = go.transform;
                }
            }
        }

        private void Update()
        {
            if (!autoQueryEveryFrame) return;

            var index = GetCurrentIndex();
            if (index == null || aoiCenter == null) return;

            _queryResults.Clear();
            index.QueryRadius(aoiCenter.position, aoiRadius, aoiLayerMask, _queryResults);
        }

        private void OnDrawGizmos()
        {
            // AOI sphere
            if (drawAoi && aoiCenter != null)
            {
                Gizmos.color = aoiColor;
                Gizmos.DrawSphere(aoiCenter.position, aoiRadius);

                Gizmos.color = new Color(aoiColor.r, aoiColor.g, aoiColor.b, 0.9f);
                Gizmos.DrawWireSphere(aoiCenter.position, aoiRadius);
            }

            // Grid (approximate, centered on AOI or this object)
            if (drawGrid)
            {
                Vector3 center = aoiCenter != null ? aoiCenter.position : transform.position;
                DrawGrid(center);
            }

            // Hitboxes
            if (drawHitboxes)
            {
                Gizmos.color = hitboxColor;
                foreach (var kv in _debugBounds)
                {
                    var b = kv.Value;
                    Vector3 size = b.Size;
                    Vector3 center = b.Center;
                    Gizmos.DrawWireCube(center, size);
                }
            }

            // Query results highlight
            if (drawAoi && _queryResults.Count > 0)
            {
                Gizmos.color = aoiResultColor;
                foreach (int id in _queryResults)
                {
                    if (_debugBounds.TryGetValue(id, out var b))
                    {
                        Gizmos.DrawWireCube(b.Center, b.Size * 1.15f);
                    }
                }
            }
        }

        private void DrawGrid(Vector3 center)
        {
            Gizmos.color = gridColor;
            float cs = gridCellSize;
            int r = gridRange;

            // Snap center to grid
            float startX = Mathf.Floor(center.x / cs) * cs - r * cs;
            float startZ = Mathf.Floor(center.z / cs) * cs - r * cs;

            for (int x = 0; x <= r * 2; x++)
            {
                float px = startX + x * cs;
                Vector3 a = new Vector3(px, center.y, startZ);
                Vector3 b = new Vector3(px, center.y, startZ + r * 2 * cs);
                Gizmos.DrawLine(a, b);
            }

            for (int z = 0; z <= r * 2; z++)
            {
                float pz = startZ + z * cs;
                Vector3 a = new Vector3(startX, center.y, pz);
                Vector3 b = new Vector3(startX + r * 2 * cs, center.y, pz);
                Gizmos.DrawLine(a, b);
            }
        }

        // Public API for manual query (if autoQueryEveryFrame is off)
        public void ManualQuery()
        {
            var index = GetCurrentIndex();
            if (index == null || aoiCenter == null) return;

            _queryResults.Clear();
            index.QueryRadius(aoiCenter.position, aoiRadius, aoiLayerMask, _queryResults);
        }

        public IReadOnlyList<int> LastQueryResults => _queryResults;
    }
}