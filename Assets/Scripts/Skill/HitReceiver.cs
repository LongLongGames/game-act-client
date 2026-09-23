// Assets/Scripts/Skill/HitReceiver.cs
using UnityEngine;
using GameAct.Spatial;

namespace GameAct.Skill
{
    /// <summary>
    /// 命中闪色：优先驱动 HitFlashLit._HitAmount；结束时 ClearPropertyBlock，避免 residual 把模型染黑。
    /// 击退：走 MonsterKnockbackService 改权威 ActMonster 位置（只改 transform 会被 LES 每帧覆盖）。
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

        /// <summary>已死亡后忽略后续命中。</summary>
        public bool IsDead { get; private set; }

        /// <summary>HP 归零时触发一次（View 播死亡动画 / 请求销毁实体）。</summary>
        public event System.Action<HitReceiver> Died;

        [Header("Hit Flash")]
        public Color HitFlashColor = new Color(1f, 0.2f, 0.12f, 1f);
        public float FlashDuration = 0.12f;
        public Renderer[] TargetRenderers;

        [Header("Knockback（目标端）")]
        /// <summary>
        /// 是否可被击退。
        /// 后期：Boss / 大型怪设为 false，或由体重表驱动（Weight ≥ 阈值 → 免疫）。
        /// ZomBunny 等普通怪保持 true。
        /// </summary>
        public bool CanBeKnockedBack = true;

        /// <summary>
        /// 击退抗性系数 [0,1]。实际位移 = 输入距离 × (1 - KnockbackResistance)。
        /// 后期可改为 f(体重)：轻型 0、中型 0.3、重型 0.7、Boss 1.0。
        /// </summary>
        [Range(0f, 1f)]
        public float KnockbackResistance = 0f;

        /// <summary>
        /// 体重占位（后期与 Monster 表挂钩）。
        /// 当前不参与计算，仅预留；Boss/大型怪可设很大并配合 CanBeKnockedBack=false。
        /// </summary>
        public float Weight = 1f;

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
            if (IsDead) return;

            CurrentHp -= skill.BaseDamage;
            Debug.Log($"[HitReceiver] Entity={EntityId} Name={gameObject.name} " +
                      $"Dmg={skill.BaseDamage} Hp={CurrentHp:F0}/{MaxHp} Skill={skill.Name}");

            _flashTimer = FlashDuration;
            ApplyFlash(1f);

            if (CurrentHp <= 0f)
                BeginDeath();
        }

        /// <summary>
        /// 应用击退。
        /// direction：击退方向（会归一化并压平到 XZ）；distance：施法端输出距离（米）。
        ///
        /// 实现要点：LES 每帧用 ActMonster.Position 覆盖 View.transform，
        /// 因此必须通过 MonsterKnockbackService 改权威位置，不能只改 transform。
        ///
        /// 后期设计（保留说明）：
        /// - 实际距离 = distance × (1 - KnockbackResistance)；
        /// - Boss / 大型怪：CanBeKnockedBack=false 短路；
        /// - Weight 由表驱动抗性与免疫。
        /// </summary>
        public void ApplyKnockback(Vector3 direction, float distance)
        {
            if (IsDead) return;
            if (distance <= 0f) return;

            // Boss / 大型怪：无法击退（后期由体重或配置写死）
            if (!CanBeKnockedBack)
            {
                Debug.Log($"[HitReceiver] Entity={EntityId} 免疫击退 (CanBeKnockedBack=false, Weight={Weight})");
                return;
            }

            float resist = Mathf.Clamp01(KnockbackResistance);
            float finalDist = distance * (1f - resist);
            if (finalDist <= 0.001f) return;

            Vector3 dir = direction;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-8f)
                dir = transform.forward;
            dir.Normalize();

            // 权威：改 ActMonster（Solo/Host 已注册回调）
            MonsterKnockbackService.RequestKnockback(EntityId, dir, finalDist);

            Debug.Log($"[HitReceiver] Entity={EntityId} Knockback request dist={finalDist:F2} (in={distance:F2} resist={resist:F2})");
        }

        void BeginDeath()
        {
            if (IsDead) return;
            IsDead = true;
            CurrentHp = 0f;
            ClearFlash();
            CombatTargetRegistry.Unregister(this);
            Debug.Log($"[HitReceiver] Entity={EntityId} 死亡 → Died 事件");
            Died?.Invoke(this);
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
