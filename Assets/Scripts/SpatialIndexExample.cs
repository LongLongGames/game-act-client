using System.Collections.Generic;
using UnityEngine;

namespace GameAct.Spatial
{
    /// <summary>
    /// Minimal usage example / test harness.
    /// Attach to any GameObject and press Play to see basic insert + query.
    /// </summary>
    public class SpatialIndexExample : MonoBehaviour
    {
        [Header("Settings")]
        public float cellSize = 10f;
        public int testEntityCount = 50;
        public float queryRadius = 15f;

        private ISpatialIndex _index;
        private readonly List<int> _queryResults = new List<int>(64);
        private readonly Dictionary<int, Vector3> _positions = new Dictionary<int, Vector3>();

        private void Start()
        {
            // Create the spatial index (easy to swap implementation later)
            _index = new SpatialHash(cellSize);

            // Spawn some dummy entities
            for (int i = 0; i < testEntityCount; i++)
            {
                Vector3 pos = new Vector3(
                    Random.Range(-50f, 50f),
                    0f,
                    Random.Range(-50f, 50f)
                );
                _positions[i] = pos;

                var bounds = AABB.FromCenterSize(pos, new Vector3(1f, 2f, 1f));
                SpatialLayer layer = (i % 5 == 0) ? SpatialLayer.LowAerial : SpatialLayer.Ground;
                _index.Insert(i, bounds, layer);
            }

            Debug.Log($"[SpatialIndexExample] Inserted {_index.Count} entities.");
        }

        private void Update()
        {
            // Example: query around this GameObject every frame
            _queryResults.Clear();
            _index.QueryRadius(transform.position, queryRadius, SpatialLayer.All, _queryResults);

            // Move a few entities randomly to test Update
            if (Time.frameCount % 30 == 0)
            {
                for (int i = 0; i < 5; i++)
                {
                    int id = Random.Range(0, testEntityCount);
                    if (!_positions.TryGetValue(id, out var pos)) continue;

                    pos += new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
                    _positions[id] = pos;

                    var bounds = AABB.FromCenterSize(pos, new Vector3(1f, 2f, 1f));
                    _index.Update(id, bounds);
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (_index == null) return;

            // Draw query sphere
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawSphere(transform.position, queryRadius);

            // Draw results
            Gizmos.color = Color.yellow;
            foreach (int id in _queryResults)
            {
                if (_positions.TryGetValue(id, out var pos))
                {
                    Gizmos.DrawWireCube(pos, new Vector3(1.2f, 2.2f, 1.2f));
                }
            }
        }
    }
}