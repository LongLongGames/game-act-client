using System.Collections;
using UnityEngine;
using GameAct.Gameplay.Character;
using GameAct.Spatial;
using GameAct.Skill;

namespace GameAct.Les.View
{
    /// <summary>
    /// Monster 表现。死亡：停注册 → 请求权威销毁实体 → 播 Dead → 延迟回收。
    /// Prefab Animator 需有 Trigger「Dead」（可改 DeathTriggerName）。
    /// </summary>
    public class MonsterView : MonoBehaviour
    {
        public ushort EntityId { get; private set; }

        [Header("Death")]
        [Tooltip("Animator Trigger 名，需与死亡动画状态机一致")]
        public string DeathTriggerName = "Dead";
        [Tooltip("死亡动画播完后回收 View 的等待（秒）")]
        public float DeathDespawnDelay = 2.2f;

        Animator _anim;
        float _walkThreshold = 0.15f;
        HitReceiver _hitReceiver;
        bool _dying;

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int MovementHash = Animator.StringToHash("Movement");
        int _deathTriggerHash;

        public static MonsterView Create(ushort entityId, Vector3 position, Transform parent = null)
        {
            var go = CharacterPrefabLoader.InstantiateMonster(position, Quaternion.identity, parent);
            if (go == null)
            {
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
            _dying = false;
            _deathTriggerHash = Animator.StringToHash(DeathTriggerName);

            _anim = GetComponentInChildren<Animator>();
            if (_anim == null)
                Debug.LogWarning($"[MonsterView] {name} 上未找到 Animator");
            else
            {
                if (HasParam(_anim, SpeedHash))
                    _anim.SetFloat(SpeedHash, 0f);
                if (HasParam(_anim, MovementHash))
                    _anim.SetFloat(MovementHash, 0f);
            }

            var authoring = GetComponent<LogicColliderAuthoring>();
            LogicCollider logic = null;
            if (authoring != null)
                logic = authoring.BuildRuntimeCollider();
            else
                logic = GetComponent<LogicCollider>();

            _hitReceiver = GetComponent<HitReceiver>();
            if (_hitReceiver == null)
                _hitReceiver = gameObject.AddComponent<HitReceiver>();

            _hitReceiver.Died -= OnReceiverDied;
            _hitReceiver.Died += OnReceiverDied;

            CombatTargetRegistry.Register(entityId, _hitReceiver, logic);
        }

        void OnReceiverDied(HitReceiver recv)
        {
            if (_dying) return;
            _dying = true;

            // 权威：停 AI + 销毁 ActMonster（同进程 Solo/Host）
            MonsterDeathService.RequestDestroy(EntityId);

            // 表现：死亡动画
            if (_anim != null && HasParam(_anim, _deathTriggerHash))
                _anim.SetTrigger(_deathTriggerHash);
            else if (_anim != null)
                Debug.LogWarning($"[MonsterView] Animator 无 Trigger「{DeathTriggerName}」，直接延迟回收");

            // 停止移动混合
            if (_anim != null)
            {
                if (HasParam(_anim, SpeedHash))
                    _anim.SetFloat(SpeedHash, 0f);
                if (HasParam(_anim, MovementHash))
                    _anim.SetFloat(MovementHash, 0f);
            }

            StartCoroutine(DespawnAfterDelay());
        }

        IEnumerator DespawnAfterDelay()
        {
            float wait = Mathf.Max(0.05f, DeathDespawnDelay);
            yield return new WaitForSeconds(wait);
            if (this != null && gameObject != null)
                Destroy(gameObject);
        }

        void OnDisable()
        {
            if (_hitReceiver != null)
            {
                _hitReceiver.Died -= OnReceiverDied;
                CombatTargetRegistry.Unregister(_hitReceiver);
            }
        }

        void OnDestroy()
        {
            if (_hitReceiver != null)
            {
                _hitReceiver.Died -= OnReceiverDied;
                CombatTargetRegistry.Unregister(_hitReceiver);
            }
        }

        public void Apply(Vector3 position, float yawDegrees, float speedXZ = 0f)
        {
            if (_dying) return; // 死亡后不再被 LES 位姿覆盖，避免「死了还走」

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
