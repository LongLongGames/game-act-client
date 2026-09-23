using UnityEngine;
using UnityEngine.InputSystem;
using GameAct.Spatial;
using GameAct.Gameplay.Player;

namespace GameAct.Skill
{
    /// <summary>
    /// 正式战斗入口：本地玩家 SkillCaster + CombatTargetRegistry 命中。
    /// 由 GameplayRunner 在会话启动后 Bind(PlayerView, entityId)。
    /// Gizmo 也画在这里（Scene 视图打开 Gizmos；不依赖 SkillSystemExample）。
    /// </summary>
    public class PlayerCombatDriver : MonoBehaviour
    {
        public static PlayerCombatDriver Instance { get; private set; }

        [Header("Optional debug")]
        public SpatialDebugDrawer debugDrawer;
        public bool ShowAlwaysPreview = true;
        public float CastGizmoDuration = 0.7f;

        [Header("Knockback（Caster 可调）")]
        [Tooltip("玩家默认击退距离（米）。技能 Define.KnockbackDistance≥0 时优先用技能值。后期与 Monster 体重挂钩。")]
        public float DefaultKnockbackDistance = 1.2f;

        ISpatialIndex _spatial;
        IHitSystem _hitSystem;
        SkillCaster _caster;
        PlayerView _playerView;
        int _casterEntityId;
        bool _ready;

        SkillDefine _defMelee;
        SkillDefine _defHitscan;
        SkillDefine _defProjectile;
        SkillDefine _defMeteor;
        SkillDefine _defZone;

        int _lastSkillId;
        float _gizmoShowTime;
        Vector3 _gizmoOrigin;
        Vector3 _gizmoForward;
        Vector3 _gizmoTarget;
        float _gizmoRange;
        float _gizmoRadius;

        public SkillCaster Caster => _caster;
        public IHitSystem HitSystem => _hitSystem;
        public bool IsReady => _ready && _playerView != null;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PlayerCombatDriver] duplicate, destroying self");
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start() => EnsureSystems();

        void EnsureSystems()
        {
            if (_caster != null) return;

            if (debugDrawer == null)
                debugDrawer = FindObjectOfType<SpatialDebugDrawer>();

            _spatial = new SpatialHash(10f);
            var hit = new HitSystem(_spatial);
            hit.GetEntityBounds = id => CombatTargetRegistry.GetBounds(id);
            _hitSystem = hit;

            _defMelee = SkillDefine.CreateMelee("Slash", 2.8f, 15f);
            _defHitscan = SkillDefine.CreateHitscan("Rail", 35f, 30f);
            _defProjectile = SkillDefine.CreateProjectile("Fireball", 16f, 22f);
            _defMeteor = SkillDefine.CreateDelayedArea("Meteor", 4.5f, 1.0f, 50f);
            _defZone = SkillDefine.CreatePersistentZone("FireZone", 3.5f, 4f, 8f);

            _caster = new SkillCaster();
            _caster.KnockbackDistance = DefaultKnockbackDistance;
            _caster.AddSkill(_defMelee);
            _caster.AddSkill(_defHitscan);
            _caster.AddSkill(_defProjectile);
            _caster.AddSkill(_defMeteor);
            _caster.AddSkill(_defZone);

            if (debugDrawer != null)
                debugDrawer.SetSpatialIndex(_spatial);

            Debug.Log("[PlayerCombatDriver] systems ready (1 Melee / 2 Rail / 3 Fireball / 4 Meteor / 5 Zone) " +
                      $"Knockback={DefaultKnockbackDistance:F2}");
        }

        public void Bind(PlayerView view, int casterEntityId)
        {
            EnsureSystems();
            _playerView = view;
            _casterEntityId = casterEntityId;
            _ready = view != null;

            if (_caster != null)
                _caster.KnockbackDistance = DefaultKnockbackDistance;

            if (debugDrawer != null && view != null)
                debugDrawer.aoiCenter = view.transform;

            Debug.Log(_ready
                ? $"[PlayerCombatDriver] Bound view={view.name} entityId={casterEntityId}"
                : "[PlayerCombatDriver] Bind failed: null PlayerView");
        }

        public void Unbind()
        {
            _playerView = null;
            _casterEntityId = 0;
            _ready = false;
        }

        void Update()
        {
            if (!_ready || _playerView == null || _caster == null)
                return;

            // Inspector 运行时改 DefaultKnockbackDistance 可即时生效
            _caster.KnockbackDistance = DefaultKnockbackDistance;

            CombatTargetRegistry.SyncToSpatial(_spatial);
            float dt = Time.deltaTime;
            _caster.Tick(dt);
            if (_gizmoShowTime > 0f)
                _gizmoShowTime -= dt;

            var t = _playerView.transform;
            Vector3 pos = t.position;
            Vector3 fwd = t.forward;
            if (fwd.sqrMagnitude < 1e-6f)
                fwd = Vector3.forward;
            else
                fwd.Normalize();

            // KnockbackDistance 在每次 Cast 前按技能解析写入 ctx
            var ctx = new SkillCastContext
            {
                CasterEntityId = _casterEntityId,
                CasterPosition = pos,
                CasterForward = fwd,
                TargetPosition = pos + fwd * 10f,
                HitSystem = _hitSystem,
                OnHit = OnSkillHit,
                KnockbackDistance = 0f // 下面按技能覆盖
            };

            var kb = Keyboard.current;
            var mouse = Mouse.current;

            bool fireMelee = (kb != null && kb.digit1Key.wasPressedThisFrame)
                             || (mouse != null && mouse.leftButton.wasPressedThisFrame);
            if (fireMelee)
            {
                ctx.KnockbackDistance = _caster.ResolveKnockback(_defMelee);
                if (_caster.TryCast(1, ctx))
                {
                    _playerView.TriggerAttack();
                    CaptureGizmo(1, pos, fwd, _defMelee);
                }
            }

            if (kb == null) return;
            if (kb.digit2Key.wasPressedThisFrame)
            {
                ctx.KnockbackDistance = _caster.ResolveKnockback(_defHitscan);
                if (_caster.TryCast(2, ctx))
                    CaptureGizmo(2, pos, fwd, _defHitscan);
            }
            if (kb.digit3Key.wasPressedThisFrame)
            {
                ctx.KnockbackDistance = _caster.ResolveKnockback(_defProjectile);
                if (_caster.TryCast(3, ctx))
                    CaptureGizmo(3, pos, fwd, _defProjectile);
            }
            if (kb.digit4Key.wasPressedThisFrame)
            {
                ctx.KnockbackDistance = _caster.ResolveKnockback(_defMeteor);
                if (_caster.TryCast(4, ctx))
                    CaptureGizmo(4, pos, fwd, _defMeteor);
            }
            if (kb.digit5Key.wasPressedThisFrame)
            {
                ctx.KnockbackDistance = _caster.ResolveKnockback(_defZone);
                if (_caster.TryCast(5, ctx))
                    CaptureGizmo(5, pos, fwd, _defZone);
            }
        }

        void CaptureGizmo(int skillId, Vector3 origin, Vector3 forward, SkillDefine def)
        {
            _lastSkillId = skillId;
            _gizmoShowTime = CastGizmoDuration;
            _gizmoOrigin = origin;
            _gizmoForward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            _gizmoRange = def != null ? def.Range : 3f;

            if (def == null) return;
            switch (def.ExecType)
            {
                case SkillExecType.Melee:
                    _gizmoRadius = def.Range * 0.6f;
                    _gizmoTarget = origin + _gizmoForward * (def.Range * 0.5f);
                    break;
                case SkillExecType.Hitscan:
                    _gizmoRadius = 0.15f;
                    _gizmoTarget = origin + _gizmoForward * def.Range;
                    break;
                case SkillExecType.Projectile:
                    _gizmoRadius = Mathf.Max(0.35f, def.ProjectileRadius);
                    _gizmoTarget = origin + _gizmoForward * Mathf.Min(def.Range, 12f);
                    break;
                case SkillExecType.DelayedArea:
                case SkillExecType.PersistentZone:
                    _gizmoRadius = def.Range;
                    _gizmoTarget = origin + _gizmoForward * Mathf.Clamp(def.Range * 1.2f, 4f, 14f);
                    _gizmoTarget.y = origin.y;
                    break;
                default:
                    _gizmoRadius = def.Range;
                    break;
            }
        }

        void OnSkillHit(int casterId, HitResult hit, SkillDefine def)
        {
            Debug.Log($"[PlayerCombat] Skill={def.Name} Target={hit.TargetEntityId} Dist={hit.Distance:F1} Dmg={def.BaseDamage}");
            if (!CombatTargetRegistry.TryGetReceiver(hit.TargetEntityId, out var receiver) || receiver == null)
                return;

            receiver.OnHit(hit, def);

            // 击退：方向优先 caster→target，否则用 CasterForward
            float kbDist = 0f;
            if (_caster != null)
                kbDist = _caster.ResolveKnockback(def);
            if (kbDist <= 0f) return;

            Vector3 from = _playerView != null ? _playerView.transform.position : hit.HitPoint;
            Vector3 dir = receiver.transform.position - from;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f && _playerView != null)
                dir = _playerView.transform.forward;
            receiver.ApplyKnockback(dir, kbDist);
        }

        void OnDrawGizmos()
        {
            if (!Application.isPlaying || _playerView == null)
                return;

            Vector3 origin = _playerView.transform.position;
            Vector3 fwd = _playerView.transform.forward;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            else fwd.Normalize();

            if (ShowAlwaysPreview)
            {
                DrawMelee(origin, fwd, _defMelee != null ? _defMelee.Range : 2.8f,
                    new Color(1f, 0.55f, 0.1f, 0.12f), new Color(1f, 0.55f, 0.1f, 0.45f));

                float rayLen = _defHitscan != null ? _defHitscan.Range : 35f;
                Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.35f);
                Gizmos.DrawLine(origin + Vector3.up * 1.2f, origin + Vector3.up * 1.2f + fwd * Mathf.Min(rayLen, 20f));

                float areaR = _defMeteor != null ? _defMeteor.Range : 4.5f;
                Vector3 pred = origin + fwd * 8f;
                pred.y = origin.y;
                Gizmos.color = new Color(1f, 0.2f, 0.9f, 0.2f);
                DrawWireCircle(pred, areaR * 0.5f);
            }

            if (_gizmoShowTime <= 0f) return;

            switch (_lastSkillId)
            {
                case 1:
                    DrawMelee(_gizmoOrigin, _gizmoForward, _gizmoRange,
                        new Color(1f, 0.2f, 0.1f, 0.3f), new Color(1f, 0.3f, 0.1f, 0.9f));
                    break;
                case 2:
                    Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.95f);
                    Gizmos.DrawLine(_gizmoOrigin + Vector3.up * 1.2f, _gizmoTarget + Vector3.up * 1.2f);
                    Gizmos.DrawSphere(_gizmoTarget + Vector3.up * 1.2f, 0.2f);
                    break;
                case 3:
                    Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.9f);
                    Gizmos.DrawLine(_gizmoOrigin + Vector3.up * 1.2f, _gizmoTarget + Vector3.up * 1.2f);
                    Gizmos.DrawWireSphere(_gizmoTarget + Vector3.up * 1.2f, _gizmoRadius);
                    Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.25f);
                    Gizmos.DrawSphere(_gizmoTarget + Vector3.up * 1.2f, _gizmoRadius);
                    break;
                case 4:
                    Gizmos.color = new Color(1f, 0.15f, 0.8f, 0.85f);
                    DrawWireCircle(_gizmoTarget, _gizmoRadius);
                    Gizmos.color = new Color(1f, 0.15f, 0.8f, 0.22f);
                    Gizmos.DrawSphere(_gizmoTarget + Vector3.up * 0.05f, _gizmoRadius);
                    Gizmos.color = new Color(1f, 0.5f, 0.9f, 0.6f);
                    Gizmos.DrawLine(_gizmoOrigin + Vector3.up, _gizmoTarget + Vector3.up);
                    break;
                case 5:
                    Gizmos.color = new Color(1f, 0.4f, 0.05f, 0.9f);
                    DrawWireCircle(_gizmoTarget, _gizmoRadius);
                    Gizmos.color = new Color(1f, 0.35f, 0.05f, 0.2f);
                    Gizmos.DrawSphere(_gizmoTarget + Vector3.up * 0.05f, _gizmoRadius);
                    break;
            }
        }

        static void DrawMelee(Vector3 origin, Vector3 forward, float range, Color fill, Color wire)
        {
            Vector3 dir = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            Vector3 center = origin + dir * (range * 0.5f);
            float radius = range * 0.6f;
            Gizmos.color = fill;
            Gizmos.DrawSphere(center, radius);
            Gizmos.color = wire;
            Gizmos.DrawWireSphere(center, radius);
            Gizmos.DrawLine(origin, origin + dir * range);
        }

        static void DrawWireCircle(Vector3 center, float radius, int segments = 32)
        {
            float step = Mathf.PI * 2f / segments;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = step * i;
                Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
