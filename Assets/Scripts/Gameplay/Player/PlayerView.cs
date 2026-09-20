using UnityEngine;
using GameAct.Gameplay.Simulation;

namespace GameAct.Gameplay.Player
{
    /// <summary>
    /// 玩家表现：Y_Bot + 本地 CC。
    /// Animator 不连 Transition，按速度 CrossFade 切 idle / walk。
    /// </summary>
    public class PlayerView : MonoBehaviour
    {
        public int EntityId { get; set; } = -1;
        public bool IsLocal { get; private set; }

        CharacterController _cc;
        Animator _anim;
        string _currentAnim;
        float _crossFade = 0.15f;
        float _walkThreshold = 0.15f; // 水平速度超过则 walk

        const string AnimIdle = "idle";
        const string AnimWalk = "walk";

        public CharacterController CharacterController => _cc;

        public void Setup(int entityId, bool isLocal)
        {
            EntityId = entityId;
            IsLocal = isLocal;
            name = isLocal ? "Player_Local" : $"Player_{entityId}";

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
                Debug.LogError("[PlayerView] 未找到 Y_Bot.prefab。路径应为 Assets/Bundles/Character/Y_Bot.prefab");
            }

            // 仅本地加 CC
            if (isLocal)
            {
                _cc = gameObject.GetComponent<CharacterController>();
                if (_cc == null) _cc = gameObject.AddComponent<CharacterController>();
                _cc.height = 1.8f;
                _cc.radius = 0.28f;
                _cc.center = new Vector3(0f, 0.9f, 0f);
                _cc.skinWidth = 0.08f;
                _cc.minMoveDistance = 0f;
                _cc.slopeLimit = 45f;
                _cc.stepOffset = 0.3f;
                _cc.enabled = false;
            }

            // 默认 idle（无连线，直接 Play）
            PlayAnim(AnimIdle, 0f);
        }

        public void EnableController()
        {
            if (_cc != null) _cc.enabled = true;
        }

        public void ApplyPose(in EntityPose pose)
        {
            if (_cc != null && _cc.enabled && IsLocal)
                transform.rotation = pose.Rotation;
            else
                transform.SetPositionAndRotation(pose.Position, pose.Rotation);

            // 水平速度 → idle / walk，不依赖 Animator 连线
            float speedXZ = new Vector2(pose.Velocity.x, pose.Velocity.z).magnitude;
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

        /// <summary>无 Transition 时用 CrossFade；同状态不重复切。</summary>
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
