using System.Collections.Generic;
using UnityEngine;

namespace GameAct.Spatial
{
    /// <summary>
    /// Minimal runtime example / smoke test for AoiSystem.
    /// Attach to any GameObject in a test scene.
    /// 
    /// Demonstrates:
    /// - Creating SpatialHash + AoiSystem
    /// - Registering one observer (player)
    /// - Spawning dummy entities and updating positions
    /// - Logging tier changes
    /// </summary>
    public class AoiSystemExample : MonoBehaviour
    {
        [Header("AOI Config")]
        public float nearRadius = 25f;
        public float midRadius  = 55f;
        public float farRadius  = 120f;

        [Header("Test")]
        public int entityCount = 40;
        public float worldHalfExtent = 80f;
        public Transform playerTransform;

        private ISpatialIndex _spatial;
        private AoiSystem _aoi;
        private readonly Dictionary<int, Vector3> _positions = new Dictionary<int, Vector3>();
        private readonly List<int> _nearBuf = new List<int>(64);

        private void Start()
        {
            _spatial = new SpatialHash(cellSize: 10f);

            var cfg = AoiConfig.Default;
            cfg.NearRadius = nearRadius;
            cfg.MidRadius  = midRadius;
            cfg.FarRadius  = farRadius;

            _aoi = new AoiSystem(cfg);
            _aoi.SetSpatial(_spatial);
            _aoi.OnTierChanged += OnTierChanged;

            // Observer = local player (id 0)
            Vector3 playerPos = playerTransform != null ? playerTransform.position : transform.position;
            _aoi.AddObserver(0, playerPos);

            // Spawn dummy entities
            for (int i = 1; i <= entityCount; i++)
            {
                Vector3 pos = new Vector3(
                    Random.Range(-worldHalfExtent, worldHalfExtent),
                    0f,
                    Random.Range(-worldHalfExtent, worldHalfExtent));

                _positions[i] = pos;
                var bounds = AABB.FromCenterSize(pos, new Vector3(1f, 2f, 1f));
                _spatial.Insert(i, bounds, SpatialLayer.Ground);
                _aoi.UpdateEntityPosition(i, pos);

                // Mark a few as swarm (client-only)
                if (i % 7 == 0)
                    _aoi.MarkAsSwarm(i);
            }

            Debug.Log($"[AoiSystemExample] Ready. Entities={entityCount}, Observer at {playerPos}");
        }

        private void Update()
        {
            if (_aoi == null) return;

            // Update observer position
            Vector3 playerPos = playerTransform != null ? playerTransform.position : transform.position;
            if (_aoi.TryGetObserver(0, out var obs))
                obs.SetPosition(playerPos);

            // Jiggle a few entities
            if (Time.frameCount % 15 == 0)
            {
                for (int n = 0; n < 5; n++)
                {
                    int id = Random.Range(1, entityCount + 1);
                    if (!_positions.TryGetValue(id, out var pos)) continue;

                    pos += new Vector3(Random.Range(-0.8f, 0.8f), 0f, Random.Range(-0.8f, 0.8f));
                    _positions[id] = pos;
                    _spatial.Update(id, AABB.FromCenterSize(pos, new Vector3(1f, 2f, 1f)));
                    _aoi.UpdateEntityPosition(id, pos);
                }
            }

            // Tick AOI
            _aoi.Tick();

            // Optional: print near count every 2s
            if (Time.frameCount % 120 == 0)
            {
                _nearBuf.Clear();
                _aoi.GetEntitiesInTier(0, AoiTier.Near, _nearBuf);
                Debug.Log($"[AoiSystemExample] Near={_nearBuf.Count}");
            }
        }

        private void OnTierChanged(int observerId, int entityId, AoiTier oldTier, AoiTier newTier)
        {
            // Keep log quiet in production; enable when debugging
            // Debug.Log($"[AOI] obs={observerId} ent={entityId} {oldTier} → {newTier}");
        }

        private void OnDestroy()
        {
            if (_aoi != null)
            {
                _aoi.OnTierChanged -= OnTierChanged;
                _aoi.Clear();
            }
            _spatial?.Clear();
        }
    }
}
