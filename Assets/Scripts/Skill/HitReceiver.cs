// Assets/Scripts/Skill/HitReceiver.cs
using UnityEngine;
using GameAct.Spatial;

namespace GameAct.Skill
{
    /// <summary>
    /// 命中闪色：优先驱动 HitFlashLit._HitAmount；结束时 ClearPropertyBlock，避免 residual 把模型染黑。
    /// </summary>
    [RequireComponent(typeof(LogicCollider))]
    public class HitReceiver : MonoBehaviour
    {
        static readonly int HitAmountId = Shader.PropertyToID("_HitAmount");
        static readonly int HitColorId = Shader.PropertyToID("_HitColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public int EntityId { get; set; } = -1;

        [Header("Stats")]
        public float MaxHp = 100f;
        public float CurrentHp = 100f;

        [Header("Hit Flash")]
        public Color HitFlashColor = new Color(1f, 0.2f, 0.12f, 1f);
        public float FlashDuration = 0.12f;
        public Renderer[] TargetRenderers;

        float _flashTimer;
        MaterialPropertyBlock _mpb;
        Color[] _cachedBaseColor;
        bool[] _useHitAmount;
        bool[] _useBaseColor;
        bool[] _useColor;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (TargetRenderers == null || TargetRenderers.Length == 0)
                TargetRenderers = GetComponentsInChildren<Renderer>(true);

            int n = TargetRenderers != null ? TargetRenderers.Length : 0;
            _cachedBaseColor = new Color[n];
            _useHitAmount = new bool[n];
            _useBaseColor = new bool[n];
            _useColor = new bool[n];

            for (int i = 0; i < n; i++)
            {
                var r = TargetRenderers[i];
                if (r == null || r.sharedMaterial == null) continue;
                var mat = r.sharedMaterial;
                _useHitAmount[i] = mat.HasProperty(HitAmountId);
                _useBaseColor[i] = mat.HasProperty(BaseColorId);
                _useColor[i] = mat.HasProperty(ColorId);
                if (_useBaseColor[i])
                    _cachedBaseColor[i] = mat.GetColor(BaseColorId);
                else if (_useColor[i])
                    _cachedBaseColor[i] = mat.GetColor(ColorId);
                else
                    _cachedBaseColor[i] = Color.white;
            }
        }

        void Update()
        {
            if (_flashTimer <= 0f) return;
            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f)
            {
                _flashTimer = 0f;
                ClearFlash();
            }
            else
            {
                ApplyFlash(Mathf.Clamp01(_flashTimer / FlashDuration));
            }
        }

        public void OnHit(HitResult hit, SkillDefine skill)
        {
            CurrentHp -= skill.BaseDamage;
            Debug.Log($"[HitReceiver] Entity={EntityId} Name={gameObject.name} " +
                      $"Dmg={skill.BaseDamage} Hp={CurrentHp:F0}/{MaxHp} Skill={skill.Name}");

            _flashTimer = FlashDuration;
            ApplyFlash(1f);

            if (CurrentHp <= 0f)
            {
                Debug.Log($"[HitReceiver] Entity={EntityId} 死亡");
                ClearFlash();
                CombatTargetRegistry.Unregister(this);
                gameObject.SetActive(false);
            }
        }

        void ApplyFlash(float amount)
        {
            if (TargetRenderers == null) return;
            amount = Mathf.Clamp01(amount);

            for (int i = 0; i < TargetRenderers.Length; i++)
            {
                var r = TargetRenderers[i];
                if (r == null) continue;

                r.GetPropertyBlock(_mpb);

                if (_useHitAmount[i])
                {
                    _mpb.SetColor(HitColorId, HitFlashColor);
                    _mpb.SetFloat(HitAmountId, amount);
                    r.SetPropertyBlock(_mpb);
                }
                else if (_useBaseColor[i])
                {
                    _mpb.SetColor(BaseColorId, Color.Lerp(_cachedBaseColor[i], HitFlashColor, amount));
                    r.SetPropertyBlock(_mpb);
                }
                else if (_useColor[i])
                {
                    _mpb.SetColor(ColorId, Color.Lerp(_cachedBaseColor[i], HitFlashColor, amount));
                    r.SetPropertyBlock(_mpb);
                }
            }
        }

        void ClearFlash()
        {
            if (TargetRenderers == null) return;
            for (int i = 0; i < TargetRenderers.Length; i++)
            {
                var r = TargetRenderers[i];
                if (r == null) continue;
                r.SetPropertyBlock(null);
            }
        }

        void OnDisable()
        {
            ClearFlash();
            CombatTargetRegistry.Unregister(this);
        }
    }
}
