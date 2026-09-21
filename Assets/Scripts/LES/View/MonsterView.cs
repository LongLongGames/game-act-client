using UnityEngine;

namespace GameAct.Les.View
{
    /// <summary>
    /// Monster 表现层：使用 ZomBunny 模型，Animator.Speed 驱动 Idle → Move。
    /// </summary>
    public class MonsterView : MonoBehaviour
    {
        public ushort EntityId { get; private set; }

        Animator _anim;
        float _walkThreshold = 0.15f;

        static readonly int SpeedHash = Animator.StringToHash("Speed");

        public static MonsterView Create(ushort entityId, Vector3 position, Transform parent = null)
        {
            var go = new GameObject($"MonsterView_{entityId}");
            if (parent != null)
                go.transform.SetParent(parent, false);
            go.transform.position = position;

            var view = go.AddComponent<MonsterView>();
            view.EntityId = entityId;
            view.LoadModel();
            return view;
        }

        void LoadModel()
        {
            var model = LoadZomBunnyModel();
            if (model != null)
            {
                model.name = "ZomBunny";
                model.transform.SetParent(transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                _anim = model.GetComponentInChildren<Animator>();
                if (_anim == null)
                    Debug.LogWarning("[MonsterView] ZomBunny 上未找到 Animator");
                else
                    _anim.SetFloat(SpeedHash, 0f);
            }
            else
            {
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name = "FallbackCapsule";
                capsule.transform.SetParent(transform, false);
                capsule.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
                var col = capsule.GetComponent<Collider>();
                if (col != null) col.isTrigger = true;
                var rend = capsule.GetComponent<Renderer>();
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
                Debug.LogWarning("[MonsterView] 未找到 ZomBunny.prefab，使用胶囊体 fallback");
            }
        }

        public void Apply(Vector3 position, float yawDegrees, float speedXZ = 0f)
        {
            transform.SetPositionAndRotation(
                position,
                Quaternion.Euler(0f, yawDegrees, 0f));

            if (_anim != null)
                _anim.SetFloat(SpeedHash, speedXZ >= _walkThreshold ? 1f : 0f);
        }

        static GameObject LoadZomBunnyModel()
        {
            var res = Resources.Load<GameObject>("Character/Monster/ZomBunny");
            if (res != null) return Object.Instantiate(res);

#if UNITY_EDITOR
            var adType = System.Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            if (adType != null)
            {
                var method = adType.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(System.Type) });
                if (method != null)
                {
                    var prefab = method.Invoke(null, new object[] {
                        "Assets/Bundles/Character/Monster/ZomBunny.prefab", typeof(GameObject)
                    }) as GameObject;
                    if (prefab != null) return Object.Instantiate(prefab);
                }
            }
#endif
            return null;
        }
    }
}
