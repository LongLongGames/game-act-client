using System.Collections.Generic;
using UnityEngine;

namespace GameAct.Spatial
{
    /// <summary>
    /// Updated usage example that also demonstrates SectionManager + SpatialDebugDrawer.
    /// </summary>
    public class SpatialIndexExample : MonoBehaviour
    {
        [Header("Settings")]
        public float cellSize = 10f;
        public int testEntityCount = 50;
        public float queryRadius = 15f;

        [Header("References")]
        public SpatialDebugDrawer debugDrawer;

        private SectionManager _sectionManager;
        private ISpatialIndex _index;
        private readonly Dictionary<int, Vector3> _positions = new Dictionary<int, Vector3>();

        private void Start()
        {
            // 1. Create SectionManager and one default section
            _sectionManager = new SectionManager();
            var bounds = new Bounds(Vector3.zero, new Vector3(200f, 50f, 200f));
            var section = _sectionManager.CreateSection("Main", bounds, cellSize);

            _index = section.Spatial;

            // 2. Hook debug drawer
            if (debugDrawer != null)
            {
                debugDrawer.SetSectionManager(_sectionManager);
                debugDrawer.aoiCenter = this.transform;
                debugDrawer.aoiRadius = queryRadius;
                debugDrawer.gridCellSize = cellSize;
            }

            // 3. Spawn dummy entities
            for (int i = 0; i < testEntityCount; i++)
            {
                Vector3 pos = new Vector3(
                    Random.Range(-50f, 50f),
                    0f,
                    Random.Range(-50f, 50f)
                );
                _positions[i] = pos;

                var entityBounds = AABB.FromCenterSize(pos, new Vector3(1f, 2f, 1f));
                SpatialLayer layer = (i % 5 == 0) ? SpatialLayer.LowAerial : SpatialLayer.Ground;
                _index.Insert(i, entityBounds, layer);

                // Also tell debug drawer about the bounds
                if (debugDrawer != null)
                    debugDrawer.SetEntityBounds(i, entityBounds);
            }

            Debug.Log($"[SpatialIndexExample] Section '{section.Name}' created, inserted {_index.Count} entities.");
        }

        private void Update()
        {
            // Move a few entities randomly to test Update
            if (Time.frameCount % 30 == 0)
            {
                for (int i = 0; i < 5; i++)
                {
                    int id = Random.Range(0, testEntityCount);
                    if (!_positions.TryGetValue(id, out var pos)) continue;

                    pos += new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
                    _positions[id] = pos;

                    var entityBounds = AABB.FromCenterSize(pos, new Vector3(1f, 2f, 1f));
                    _index.Update(id, entityBounds);

                    if (debugDrawer != null)
                        debugDrawer.SetEntityBounds(id, entityBounds);
                }
            }
        }
    }
}