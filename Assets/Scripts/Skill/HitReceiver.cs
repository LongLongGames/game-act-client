// Assets/Scripts/Skill/HitReceiver.cs
using UnityEngine;
using GameAct.Spatial;

namespace GameAct.Skill
{
    /// <summary>
    /// 挂在怪物 Prefab Root 上，接收技能命中。
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

        Renderer _renderer;
        Color _originalColor;
        float _flashTimer;

        void Awake()
        {
            _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null)
                _originalColor = _renderer.material.color;
        }

        void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f && _renderer != null)
                    _renderer.material.color = _originalColor;
            }
        }

        public void OnHit(HitResult hit, SkillDefine skill)
        {
            CurrentHp -= skill.BaseDamage;
            Debug.Log($"[HitReceiver] Entity={EntityId} Name={gameObject.name} " +
                      $"Dmg={skill.BaseDamage} Hp={CurrentHp:F0}/{MaxHp} Skill={skill.Name}");

            if (_renderer != null)
            {
                _renderer.material.color = HitFlashColor;
                _flashTimer = FlashDuration;
            }

            if (CurrentHp <= 0f)
            {
                Debug.Log($"[HitReceiver] Entity={EntityId} 死亡");
                CombatTargetRegistry.Unregister(this);
                gameObject.SetActive(false);
            }
        }

        void OnDisable()
        {
            CombatTargetRegistry.Unregister(this);
        }
    }
}
