using System.Collections.Generic;
using UnityEngine;
using GameAct.Gameplay.Character;
using GameAct.Spatial;

namespace GameAct.Gameplay.Player
{
    /// <summary>
    /// 纯表现层：跟 LES / 模拟位姿，不做物理。
    /// Prefab 标准：Root 挂本脚本 + LogicCollider* + HitReceiver，子节点 Model 挂 Animator。
    /// Y_Bot.prefab 使用 PlayerLoco_P2.controller：
    ///   Movement (float) / IsGrounded (bool) / IsJumping (bool) / Attack (trigger，patch 补上)
    /// </summary>
    public class PlayerView : MonoBehaviour
    {
        public int EntityId { get; set; } = -1;
        public bool IsLocal { get; private set; }

        Animator _anim;
        bool _wasGrounded = true;
        bool _isJumping;

        // 远程动作事件：对比计数变化触发（首次只采纳，不回放历史）
        byte _jumpSeen;
        byte _attackSeen;
        bool _eventsInited;

        // Animator.parameters 每次访问都会分配数组，缓存一次
        readonly Dictionary<int, AnimatorControllerParameterType> _paramCache =
            new Dictionary<int, AnimatorControllerParameterType>(8);

        const float WalkSpeedRef = 5.5f;
        const float SprintSpeedRef = 8.5f;
        const float IdleCutoff = 0.15f;

        // PlayerLoco_P2 参数
        static readonly int MovementHash = Animator.StringToHash("Movement");
        static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
        static readonly int AttackHash = Animator.StringToHash("Attack");

        /// <summary>兼容旧代码；始终为 null，禁止再绑 CC。</summary>
        public CharacterController CharacterController => null;

        public static PlayerView Create(int entityId, bool isLocal, Vector3 position, Transform parent = null)
        {
            var go = CharacterPrefabLoader.InstantiatePlayer(position, Quaternion.identity, parent);
            if (go == null)
            {
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

            var existing = GetComponent<CharacterController>();
            if (existing != null)
                Destroy(existing);

            _eventsInited = false;
            _anim = GetComponentInChildren<Animator>();
            CacheAnimatorParams();
            if (_anim == null)
            {
                Debug.LogWarning($"[PlayerView] {name} 上未找到 Animator（检查 Prefab 子节点 Model）");
            }
            else
            {
                _anim.SetFloat(MovementHash, 0f);
                SetBoolSafe(IsGroundedHash, true);
                SetBoolSafe(IsJumpingHash, false);
                _wasGrounded = true;
                _isJumping = false;

                var ctrl = _anim.runtimeAnimatorController;
                Debug.Log($"[PlayerView] AnimatorController={ctrl?.name ?? "null"} on {name}");
            }

            var authoring = GetComponent<LogicColliderAuthoring>();
            if (authoring != null)
                authoring.BuildRuntimeCollider();
        }

        public void EnableController() { }

        /// <summary>
        /// 远程玩家动作事件同步：权威端 ActPlayer 的 JumpCount / AttackCount 变化 → 播一次 Jump / Attack。
        /// 每帧调用（先于 ApplyPose）；首次调用只记录基线。
        /// </summary>
        public void SyncEvents(byte jumpCount, byte attackCount)
        {
            if (!_eventsInited)
            {
                _jumpSeen = jumpCount;
                _attackSeen = attackCount;
                _eventsInited = true;
                return;
            }

            if (jumpCount != _jumpSeen)
            {
                _jumpSeen = jumpCount;
                TriggerJump();
            }
            if (attackCount != _attackSeen)
            {
                _attackSeen = attackCount;
                TriggerAttack();
            }
        }

        void CacheAnimatorParams()
        {
            _paramCache.Clear();
            if (_anim == null || _anim.runtimeAnimatorController == null) return;
            foreach (var p in _anim.parameters)
                _paramCache[p.nameHash] = p.type;
        }

        /// <summary>
        /// 应用位姿并驱动 PlayerLoco_P2：Movement / IsGrounded / IsJumping。
        /// </summary>
        public void ApplyPose(Vector3 position, float yawDegrees, float speedXZ, bool grounded, float verticalVelocity)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDegrees, 0f));
            UpdateLocomotion(speedXZ);
            UpdateAirState(grounded, verticalVelocity);
        }

        /// <summary>兼容旧 3 参数调用。</summary>
        public void ApplyPose(Vector3 position, float yawDegrees, float speedXZ = 0f)
        {
            ApplyPose(position, yawDegrees, speedXZ, true, 0f);
        }

        void UpdateLocomotion(float speedXZ)
        {
            if (_anim == null) return;

            float movement;
            if (speedXZ < IdleCutoff)
                movement = 0f;
            else if (speedXZ <= WalkSpeedRef)
                movement = Mathf.Clamp01(speedXZ / WalkSpeedRef);
            else
            {
                float t = Mathf.Clamp01((speedXZ - WalkSpeedRef) / (SprintSpeedRef - WalkSpeedRef));
                movement = 1f + t;
            }

            _anim.SetFloat(MovementHash, movement, 0.1f, Time.deltaTime);
        }

        void UpdateAirState(bool grounded, float verticalVelocity)
        {
            if (_anim == null) return;

            bool justLeftGround = _wasGrounded && !grounded;
            bool rising = verticalVelocity > 0.5f;
            if (justLeftGround && rising)
                _isJumping = true;

            if (grounded)
                _isJumping = false;

            SetBoolSafe(IsGroundedHash, grounded);
            SetBoolSafe(IsJumpingHash, _isJumping);

            _wasGrounded = grounded;
        }

        /// <summary>
        /// 攻击：优先 Trigger「Attack」；否则 CrossFade 到 Attack 状态。
        /// </summary>
        public void TriggerAttack()
        {
            if (_anim == null) return;

            if (HasParam(AttackHash, AnimatorControllerParameterType.Trigger))
            {
                _anim.SetTrigger(AttackHash);
                return;
            }

            _anim.CrossFadeInFixedTime("Attack", 0.05f, 0, 0f);
        }

        /// <summary>逻辑起跳当帧调用，立刻置 IsJumping。</summary>
        public void TriggerJump()
        {
            _isJumping = true;
            if (_anim == null) return;
            SetBoolSafe(IsGroundedHash, false);
            SetBoolSafe(IsJumpingHash, true);
        }

        void SetBoolSafe(int hash, bool value)
        {
            if (HasParam(hash, AnimatorControllerParameterType.Bool))
                _anim.SetBool(hash, value);
        }

        bool HasParam(int hash, AnimatorControllerParameterType? type = null)
        {
            if (_anim == null || _anim.runtimeAnimatorController == null)
                return false;
            if (!_paramCache.TryGetValue(hash, out var t))
                return false;
            return type == null || t == type.Value;
        }
    }
}
