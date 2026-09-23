// Assets/Scripts/Skill/HitReceiver.cs
using UnityEngine;
using GameAct.Spatial;

namespace GameAct.Skill
{
    /// <summary>
    /// 挂在 Dummy / 怪物上，接收技能命中。
    /// 可扩展血量、受击反馈、死亡等。
    /// </summary>
    [RequireComponent(typeof(LogicCollider))]
    public class HitReceiver : MonoBehaviour
    {
        public int EntityId { get; set; } = -1;

        [Header("Stats")]
        public float MaxHp = 100f;
        public float CurrentHp = 100f;

        [Header("Feedback")]
        public Color HitFlashColor = Color.red;
        public float FlashDuration = 0.15f;

        private Renderer _renderer;
        private Color _originalColor;
        private float _flashTimer;

        private void Awake()
        {
            _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null)
                _originalColor = _renderer.material.color;
        }

        private void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f && _renderer != null)
                    _renderer.material.color = _originalColor;
            }
        }

        /// <summary>
        /// 被技能命中时调用
        /// </summary>
        public void OnHit(HitResult hit, SkillDefine skill)
        {
            CurrentHp -= skill.BaseDamage;
            Debug.Log($"[HitReceiver] Entity={EntityId} Name={gameObject.name} " +
                      $"Dmg={skill.BaseDamage} Hp={CurrentHp:F0}/{MaxHp} Skill={skill.Name}");

            // 闪红反馈
            if (_renderer != null)
            {
                _renderer.material.color = HitFlashColor;
                _flashTimer = FlashDuration;
            }

            if (CurrentHp <= 0f)
            {
                Debug.Log($"[HitReceiver] Entity={EntityId} 死亡");
                // 这里可以播死亡动画 / 回收对象池
                gameObject.SetActive(false);
            }
        }
    }
}