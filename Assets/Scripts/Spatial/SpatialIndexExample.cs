/*
using System.Collections.Generic;
using UnityEngine;

namespace GameAct.Spatial
{
    /// <summary>
    /// Full example: SectionManager + SpatialHash + LogicCollider + DebugDrawer.
    /// </summary>
    public class SpatialIndexExample : MonoBehaviour
    {
        [Header("Settings")]
        public float cellSize = 10f;
        public int testEntityCount = 30;
        public float queryRadius = 15f;

        [Header("References")]
        public SpatialDebugDrawer debugDrawer;

        private SectionManager _sectionManager;
        private ISpatialIndex _index;
        private readonly Dictionary<int, LogicCollider> _logicColliders = new Dictionary<int, LogicCollider>();
        private readonly Dictionary<int, Vector3> _positions = new Dictionary<int, Vector3>();

        private void Start()
        {
            _sectionManager = new SectionManager();
            var bounds = new Bounds(Vector3.zero, new Vector3(200f, 50f, 200f));
            var section = _sectionManager.CreateSection("Main", bounds, cellSize);
            _index = section.Spatial;

            if (debugDrawer != null)
            {
                debugDrawer.SetSectionManager(_sectionManager);
                debugDrawer.aoiCenter = this.transform;
                debugDrawer.aoiRadius = queryRadius;
                debugDrawer.gridCellSize = cellSize;
            }

            // Create dummy entities with LogicCollider
            for (int i = 0; i < testEntityCount; i++)
            {
                Vector3 pos = new Vector3(Random.Range(-40f, 40f), 0f, Random.Range(-40f, 40f));
                _positions[i] = pos;

                // Simulate a runtime entity
                var go = new GameObject($"Entity_{i}");
                go.transform.position = pos;

                var logic = go.AddComponent<LogicCollider>();
                var data = LogicColliderData.DefaultBody();

                // Make some of them spheres (like Wisps)
                if (i % 5 == 0)
                {
                    data.Shape = LogicShapeType.Sphere;
                    data.CenterOffset = new Vector3(0, 1.2f, 0);
                    data.Radius = 0.6f;
                    data.Name = "WispBody";
                }

                logic.Initialize(new[] { data });
                _logicColliders[i] = logic;

                var worldBounds = logic.GetWorldBounds();
                SpatialLayer layer = (i % 5 == 0) ? SpatialLayer.LowAerial : SpatialLayer.Ground;
                _index.Insert(i, worldBounds, layer);

                if (debugDrawer != null)
                    debugDrawer.SetEntityBounds(i, worldBounds);
            }

            Debug.Log($"[SpatialIndexExample] Inserted {_index.Count} entities with LogicCollider.");
        }

        private void Update()
        {
            if (Time.frameCount % 30 != 0) return;

            for (int i = 0; i < 5; i++)
            {
                int id = Random.Range(0, testEntityCount);
                if (!_positions.TryGetValue(id, out var pos)) continue;
                if (!_logicColliders.TryGetValue(id, out var logic)) continue;

                pos += new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
                _positions[id] = pos;
                logic.transform.position = pos;

                var worldBounds = logic.GetWorldBounds();
                _index.Update(id, worldBounds);

                if (debugDrawer != null)
                    debugDrawer.SetEntityBounds(id, worldBounds);
            }
        }
    }
}
*/
