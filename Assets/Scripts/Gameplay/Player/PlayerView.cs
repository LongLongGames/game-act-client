using UnityEngine;
using GameAct.Gameplay.Character;
using GameAct.Spatial;

namespace GameAct.Gameplay.Player
{
    /// <summary>
    /// 纯表现层：跟 LES / 模拟位姿，不做物理。
    /// Prefab 标准：Root 挂本脚本 + LogicCollider* + HitReceiver，子节点 Model 挂 Animator。
    /// 不再运行时 Load 模型。
    /// </summary>
    public class PlayerView : MonoBehaviour
    {
        public int EntityId { get; set; } = -1;
        public bool IsLocal { get; private set; }

        Animator _anim;

        const float WalkSpeedRef = 5.5f;
        const float SprintSpeedRef = 8.5f;
        const float IdleCutoff = 0.15f;

        static readonly int MovementHash = Animator.StringToHash("Movement");
        static readonly int AttackHash = Animator.StringToHash("Attack");

        /// <summary>兼容旧代码；始终为 null，禁止再绑 CC。</summary>
        public CharacterController CharacterController => null;

        /// <summary>
        /// 从标准 Prefab 实例化并 Setup。
        /// </summary>
        public static PlayerView Create(int entityId, bool isLocal, Vector3 position, Transform parent = null)
        {
            var go = CharacterPrefabLoader.InstantiatePlayer(position, Quaternion.identity, parent);
            if (go == null)
            {
                // fallback：空 Root，避免空引用崩溃
                go = new GameObject(isLocal ? "Player_Local" : $"Player_{entityId}");
                if (parent != null)
                    go.transform.SetParent(parent, false);
                go.transform.position = position;
            }

            var view = go.GetComponent<PlayerView>();
            if (view == null)
                view = go.AddComponent<PlayerView>();

            view.Setup(entityId, isLocal);
            return view;
        }

        public void Setup(int entityId, bool isLocal)
        {
            EntityId = entityId;
            IsLocal = isLocal;
            name = isLocal ? "Player_Local" : $"Player_{entityId}";

            // 若预制体上误挂了 CC，拆掉，避免和 LES 写 Transform 冲突
            var existing = GetComponent<CharacterController>();
            if (existing != null)
                Destroy(existing);

            // Prefab 已包含 Model 子节点 + Animator
            _anim = GetComponentInChildren<Animator>();
            if (_anim == null)
                Debug.LogWarning($"[PlayerView] {name} 上未找到 Animator（检查 Prefab 子节点 Model）");
            else
                _anim.SetFloat(MovementHash, 0f);

            // 若有 Authoring，确保 Runtime LogicCollider 已初始化
            var authoring = GetComponent<LogicColliderAuthoring>();
            if (authoring != null)
                authoring.BuildRuntimeCollider();
        }

        /// <summary>空实现：保留 API，避免旧调用编译失败。</summary>
        public void EnableController() { }

        public void ApplyPose(Vector3 position, float yawDegrees, float speedXZ = 0f)
        {
            transform.SetPositionAndRotation(
                position,
                Quaternion.Euler(0f, yawDegrees, 0f));
            UpdateLocomotion(speedXZ);
        }

        void UpdateLocomotion(float speedXZ)
        {
            if (_anim == null) return;

            float movement;
            if (speedXZ < IdleCutoff)
            {
                movement = 0f;
            }
            else if (speedXZ <= WalkSpeedRef)
            {
                movement = Mathf.Clamp01(speedXZ / WalkSpeedRef);
            }
            else
            {
                float t = Mathf.Clamp01((speedXZ - WalkSpeedRef) / (SprintSpeedRef - WalkSpeedRef));
                movement = 1f + t;
            }

            _anim.SetFloat(MovementHash, movement, 0.1f, Time.deltaTime);
        }

        /// <summary>触发一次攻击动画（Animator Trigger「Attack」）。</summary>
        public void TriggerAttack()
        {
            if (_anim == null) return;
            _anim.SetTrigger(AttackHash);
        }
    }
}
