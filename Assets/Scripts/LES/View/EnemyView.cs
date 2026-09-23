/* MERGED → MonsterView. 本文件整文件废弃，可自行删除。
   正式表现：Assets/Scripts/LES/View/MonsterView.cs
   Solo/Host/Client 刷怪与同步均走 MonsterView

using UnityEngine;

namespace GameAct.Les.View
{
    public class EnemyView : MonoBehaviour
    {
        public ushort EntityId { get; private set; }

        public static EnemyView Create(ushort entityId, Vector3 position, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"EnemyView_{entityId}";
            if (parent != null)
                go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);

            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                if (mat != null)
                {
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", new Color(0.85f, 0.2f, 0.15f, 1f));
                    else if (mat.HasProperty("_Color"))
                        mat.color = new Color(0.85f, 0.2f, 0.15f, 1f);
                    rend.sharedMaterial = mat;
                }
            }

            var view = go.AddComponent<EnemyView>();
            view.EntityId = entityId;
            return view;
        }

        public void Apply(Vector3 position, float yawDegrees)
        {
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
        }
    }
}

*/
