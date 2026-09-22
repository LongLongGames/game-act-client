using UnityEngine;

namespace GameAct.Gameplay.Player
{
    /// <summary>
    /// 纯表现层：跟 LES / 模拟位姿，不做物理。
    /// 不挂 CharacterController（参考 LES ClientPlayerView：只跟实体 Position）。
    /// 待机/移动由 Animator float「Movement」驱动；攻击由 Trigger「Attack」切换。
    /// </summary>
    public class PlayerView : MonoBehaviour
    {
        public int EntityId { get; set; } = -1;
        public bool IsLocal { get; private set; }

        Animator _anim;
        float _walkThreshold = 0.15f;

        static readonly int MovementHash = Animator.StringToHash("Movement");
        static readonly int AttackHash = Animator.StringToHash("Attack");

        /// <summary>兼容旧代码；始终为 null，禁止再绑 CC。</summary>
        public CharacterController CharacterController => null;

        public void Setup(int entityId, bool isLocal)
        {
            EntityId = entityId;
            IsLocal = isLocal;
            name = isLocal ? "Player_Local" : $"Player_{entityId}";

            // 若预制体上误挂了 CC，拆掉，避免和 LES 写 Transform 冲突
            var existing = GetComponent<CharacterController>();
            if (existing != null)
                Destroy(existing);

            var model = LoadYBotModel();
            if (model != null)
            {
                model.name = "Y_Bot";
                model.transform.SetParent(transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                _anim = model.GetComponentInChildren<Animator>();
                if (_anim == null)
                    Debug.LogWarning("[PlayerView] Y_Bot 上未找到 Animator");
                else
                    _anim.SetFloat(MovementHash, 0f);
            }
            else
            {
                Debug.LogError("[PlayerView] 未找到 Y_Bot.prefab");
            }
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
            // Movement: 0 = idle, >= threshold → walk（由 Animator 过渡条件控制）
            float movement = speedXZ >= _walkThreshold ? 1f : 0f;
            _anim.SetFloat(MovementHash, movement);
        }

        /// <summary>触发一次攻击动画（Animator Trigger「Attack」）。</summary>
        public void TriggerAttack()
        {
            if (_anim == null) return;
            _anim.SetTrigger(AttackHash);
        }

        static GameObject LoadYBotModel()
        {
            var res = Resources.Load<GameObject>("Character/Player/Y_Bot");
            if (res != null) return Object.Instantiate(res);

#if UNITY_EDITOR
            var adType = System.Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            if (adType != null)
            {
                var method = adType.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(System.Type) });
                if (method != null)
                {
                    var prefab = method.Invoke(null, new object[] {
                        "Assets/Bundles/Character/Player/Y_Bot.prefab", typeof(GameObject)
                    }) as GameObject;
                    if (prefab != null) return Object.Instantiate(prefab);
                }
            }
#endif
            return null;
        }
    }
}
