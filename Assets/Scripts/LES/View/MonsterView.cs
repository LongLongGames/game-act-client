using UnityEngine;
using GameAct.Gameplay.Character;
using GameAct.Spatial;

namespace GameAct.Les.View
{
    /// <summary>
    /// Monster 表现层。Prefab 标准：Root 挂本脚本 + LogicCollider* + HitReceiver，子节点 Model 挂 Animator。
    /// 不再运行时拼模型。
    /// </summary>
    public class MonsterView : MonoBehaviour
    {
        public ushort EntityId { get; private set; }

        Animator _anim;
        float _walkThreshold = 0.15f;

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int MovementHash = Animator.StringToHash("Movement");

        public static MonsterView Create(ushort entityId, Vector3 position, Transform parent = null)
        {
            var go = CharacterPrefabLoader.InstantiateMonster(position, Quaternion.identity, parent);
            if (go == null)
            {
                // fallback：空节点 + 胶囊，避免整批刷怪失败
                go = new GameObject($"MonsterView_{entityId}");
                if (parent != null)
                    go.transform.SetParent(parent, false);
                go.transform.position = position;
                CreateFallbackVisual(go.transform);
            }

            var view = go.GetComponent<MonsterView>();
            if (view == null)
                view = go.AddComponent<MonsterView>();

            view.Setup(entityId);
            return view;
        }

        public void Setup(ushort entityId)
        {
            EntityId = entityId;
            name = $"Monster_{entityId}";

            _anim = GetComponentInChildren<Animator>();
            if (_anim == null)
                Debug.LogWarning($"[MonsterView] {name} 上未找到 Animator");
            else
            {
                // 兼容 Speed 或 Movement 参数
                if (HasParam(_anim, SpeedHash))
                    _anim.SetFloat(SpeedHash, 0f);
                if (HasParam(_anim, MovementHash))
                    _anim.SetFloat(MovementHash, 0f);
            }

            var authoring = GetComponent<LogicColliderAuthoring>();
            if (authoring != null)
                authoring.BuildRuntimeCollider();
        }

        public void Apply(Vector3 position, float yawDegrees, float speedXZ = 0f)
        {
            transform.SetPositionAndRotation(
                position,
                Quaternion.Euler(0f, yawDegrees, 0f));

            if (_anim == null) return;

            float v = speedXZ >= _walkThreshold ? 1f : 0f;
            if (HasParam(_anim, SpeedHash))
                _anim.SetFloat(SpeedHash, v);
            if (HasParam(_anim, MovementHash))
                _anim.SetFloat(MovementHash, v);
        }

        static bool HasParam(Animator anim, int hash)
        {
            foreach (var p in anim.parameters)
            {
                if (p.nameHash == hash)
                    return true;
            }
            return false;
        }

        static void CreateFallbackVisual(Transform parent)
        {
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "FallbackCapsule";
            capsule.transform.SetParent(parent, false);
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
            Debug.LogWarning("[MonsterView] 未找到 Monster Prefab，使用胶囊体 fallback");
        }
    }
}
