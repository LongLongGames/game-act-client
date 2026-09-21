using UnityEngine;

namespace GameAct.Gameplay.Player
{
    /// <summary>
    /// 纯表现层：跟 LES / 模拟位姿，不做物理。
    /// 不挂 CharacterController（参考 LES ClientPlayerView：只跟实体 Position）。
    /// </summary>
    public class PlayerView : MonoBehaviour
    {
        public int EntityId { get; set; } = -1;
        public bool IsLocal { get; private set; }

        Animator _anim;
        string _currentAnim;
        float _crossFade = 0.15f;
        float _walkThreshold = 0.15f;

        const string AnimIdle = "idle";
        const string AnimWalk = "walk";

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
            }
            else
            {
                Debug.LogError("[PlayerView] 未找到 Y_Bot.prefab");
            }

            PlayAnim(AnimIdle, 0f);
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
            if (speedXZ >= _walkThreshold)
                PlayAnim(AnimWalk, _crossFade);
            else
                PlayAnim(AnimIdle, _crossFade);
        }

        void PlayAnim(string stateName, float fade)
        {
            if (_anim == null) return;
            if (_currentAnim == stateName) return;
            _currentAnim = stateName;
            if (fade <= 0f)
                _anim.Play(stateName, 0, 0f);
            else
                _anim.CrossFade(stateName, fade, 0);
        }

        static GameObject LoadYBotModel()
        {
            var res = Resources.Load<GameObject>("Character/Y_Bot");
            if (res != null) return Object.Instantiate(res);

#if UNITY_EDITOR
            var adType = System.Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            if (adType != null)
            {
                var method = adType.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(System.Type) });
                if (method != null)
                {
                    var prefab = method.Invoke(null, new object[] {
                        "Assets/Bundles/Character/Y_Bot.prefab", typeof(GameObject)
                    }) as GameObject;
                    if (prefab != null) return Object.Instantiate(prefab);
                }
            }
#endif
            return null;
        }
    }
}
