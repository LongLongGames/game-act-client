using UnityEngine;

namespace GameAct.Spatial
{
    /// <summary>
    /// Runtime logic collider component.
    /// Holds the pre-configured shapes and provides world-space AABB for Spatial system.
    /// </summary>
    public class LogicCollider : MonoBehaviour
    {
        private LogicColliderData[] _colliders;
        private bool _initialized;

        public bool IsInitialized => _initialized;
        public int ColliderCount => _colliders?.Length ?? 0;

        public void Initialize(LogicColliderData[] colliders)
        {
            _colliders = colliders;
            _initialized = true;
        }

        /// <summary>
        /// Get the combined world-space AABB of all logic colliders.
        /// Used for Spatial broadphase insertion.
        /// </summary>
        public AABB GetWorldBounds()
        {
            if (!_initialized || _colliders == null || _colliders.Length == 0)
            {
                // Fallback: small default box
                return AABB.FromCenterSize(transform.position, new Vector3(0.5f, 1f, 0.5f));
            }

            AABB combined = _colliders[0].ToWorldAABB(transform.position, transform.rotation);

            for (int i = 1; i < _colliders.Length; i++)
            {
                var b = _colliders[i].ToWorldAABB(transform.position, transform.rotation);
                combined.Encapsulate(b.Min);
                combined.Encapsulate(b.Max);
            }

            return combined;
        }

        /// <summary>
        /// Get a specific collider's world AABB by index.
        /// </summary>
        public bool TryGetWorldBounds(int index, out AABB bounds)
        {
            if (!_initialized || _colliders == null || index < 0 || index >= _colliders.Length)
            {
                bounds = default;
                return false;
            }

            bounds = _colliders[index].ToWorldAABB(transform.position, transform.rotation);
            return true;
        }

        /// <summary>
        /// Get collider data by name (e.g. "Body", "Head").
        /// </summary>
        public bool TryGetCollider(string name, out LogicColliderData data)
        {
            if (_colliders != null)
            {
                for (int i = 0; i < _colliders.Length; i++)
                {
                    if (_colliders[i].Name == name)
                    {
                        data = _colliders[i];
                        return true;
                    }
                }
            }
            data = default;
            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_initialized || _colliders == null) return;

            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
            var pos = transform.position;
            var rot = transform.rotation;

            foreach (var col in _colliders)
            {
                Vector3 worldCenter = pos + rot * col.CenterOffset;

                switch (col.Shape)
                {
                    case LogicShapeType.AABB:
                        Matrix4x4 old = Gizmos.matrix;
                        Gizmos.matrix = Matrix4x4.TRS(worldCenter, rot, Vector3.one);
                        Gizmos.DrawWireCube(Vector3.zero, col.Size);
                        Gizmos.matrix = old;
                        break;
                    case LogicShapeType.Sphere:
                        Gizmos.DrawWireSphere(worldCenter, col.Radius);
                        break;
                    case LogicShapeType.Capsule:
                        // Simple representation
                        Gizmos.DrawWireSphere(worldCenter, col.Radius);
                        break;
                }
            }
        }
#endif
    }
}