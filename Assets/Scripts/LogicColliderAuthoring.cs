using UnityEngine;

namespace GameAct.Spatial
{
    /// <summary>
    /// Prefab 上挂这个组件来预配置角色/怪物的 Logic Collider。
    /// 
    /// 使用方法：
    /// 1. 选中角色或怪物 Prefab
    /// 2. Add Component → LogicColliderAuthoring
    /// 3. 在 Inspector 里配置 Colliders 列表（可多个部位）
    /// 4. Scene 视图会实时用 Gizmos 显示体积
    /// 
    /// 运行时由 LogicCollider 组件读取这些数据。
    /// </summary>
    [DisallowMultipleComponent]
    public class LogicColliderAuthoring : MonoBehaviour
    {
        [Header("Logic Colliders (Local Space)")]
        public LogicColliderData[] Colliders = new LogicColliderData[]
        {
            LogicColliderData.DefaultBody()
        };

        [Header("Gizmos")]
        public bool drawGizmos = true;
        public Color gizmoColor = new Color(0.2f, 0.8f, 1f, 0.8f);
        public Color selectedColor = Color.yellow;

        private void OnDrawGizmos()
        {
            if (!drawGizmos || Colliders == null) return;
            DrawColliders(gizmoColor);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || Colliders == null) return;
            DrawColliders(selectedColor);
        }

        private void DrawColliders(Color color)
        {
            Gizmos.color = color;
            var pos = transform.position;
            var rot = transform.rotation;

            foreach (var col in Colliders)
            {
                Vector3 worldCenter = pos + rot * col.CenterOffset;

                switch (col.Shape)
                {
                    case LogicShapeType.AABB:
                        {
                            // Draw local AABB as OBB-ish wire cube (approximate)
                            Matrix4x4 old = Gizmos.matrix;
                            Gizmos.matrix = Matrix4x4.TRS(worldCenter, rot, Vector3.one);
                            Gizmos.DrawWireCube(Vector3.zero, col.Size);
                            Gizmos.matrix = old;
                        }
                        break;

                    case LogicShapeType.Sphere:
                        Gizmos.DrawWireSphere(worldCenter, col.Radius);
                        break;

                    case LogicShapeType.Capsule:
                        DrawWireCapsule(worldCenter, rot, col.Radius, col.Height, col.Direction);
                        break;
                }
            }
        }

        private static void DrawWireCapsule(Vector3 center, Quaternion rot, float radius, float height, int direction)
        {
            float halfH = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 axis = direction == 0 ? Vector3.right :
                           direction == 2 ? Vector3.forward : Vector3.up;
            axis = rot * axis;

            Vector3 p1 = center + axis * halfH;
            Vector3 p2 = center - axis * halfH;

            // Two spheres + lines
            Gizmos.DrawWireSphere(p1, radius);
            Gizmos.DrawWireSphere(p2, radius);

            // Simple connecting lines (4 directions)
            Vector3 right = rot * (direction == 0 ? Vector3.up : Vector3.right);
            Vector3 forward = Vector3.Cross(axis, right).normalized;
            right = Vector3.Cross(forward, axis).normalized;

            Gizmos.DrawLine(p1 + right * radius, p2 + right * radius);
            Gizmos.DrawLine(p1 - right * radius, p2 - right * radius);
            Gizmos.DrawLine(p1 + forward * radius, p2 + forward * radius);
            Gizmos.DrawLine(p1 - forward * radius, p2 - forward * radius);
        }

        /// <summary>
        /// 运行时创建 LogicCollider 组件并初始化。
        /// 可在实体生成时调用。
        /// </summary>
        public LogicCollider BuildRuntimeCollider()
        {
            var runtime = gameObject.GetComponent<LogicCollider>();
            if (runtime == null)
                runtime = gameObject.AddComponent<LogicCollider>();

            runtime.Initialize(Colliders);
            return runtime;
        }
    }
}