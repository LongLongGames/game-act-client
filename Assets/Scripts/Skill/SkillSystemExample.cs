// Assets/Scripts/Skill/SkillSystemExample.cs
// 技能输入 + CombatTargetRegistry；Gizmo：1 近战 / 2 射线 / 3 弹道 / 4 落雷区 / 5 持续圈
using UnityEngine;
using UnityEngine.InputSystem;
using GameAct.Spatial;
using GameAct.Gameplay.Player;

namespace GameAct.Skill
{
    public class SkillSystemExample : MonoBehaviour
    {
        [Header("Refs（可空，运行时自动查找）")]
        public Transform player;
        public SpatialDebugDrawer debugDrawer;

        [Header("Gizmo")]
        public bool ShowAlwaysPreview = true;
        public float CastGizmoDuration = 0.7f;

        ISpatialIndex _spatial;
        IHitSystem _hitSystem;
        SkillCaster _caster;
        bool _bound;
        PlayerView _playerView;

        int _lastSkillId;
        float _gizmoShowTime;
        Vector3 _gizmoOrigin;
        Vector3 _gizmoForward;
        Vector3 _gizmoTarget;
        float _gizmoRange;
        float _gizmoRadius;

        SkillDefine _defMelee;
        SkillDefine _defHitscan;
        SkillDefine _defProjectile;
        SkillDefine _defMeteor;
        SkillDefine _defZone;

        void Start()
        {
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
            _caster.AddSkill(_defMelee);
            _caster.AddSkill(_defHitscan);
            _caster.AddSkill(_defProjectile);
            _caster.AddSkill(_defMeteor);
            _caster.AddSkill(_defZone);

            if (debugDrawer != null)
                debugDrawer.SetSpatialIndex(_spatial);

            Debug.Log("[SkillSystemExample] Gizmo: 1 Melee / 2 Ray / 3 Projectile / 4 Area / 5 Zone");
        }

        void Update()
        {
            if (!_bound)
            {
                TryBindPlayer();
                if (!_bound) return;
            }

            CombatTargetRegistry.SyncToSpatial(_spatial);

            float dt = Time.deltaTime;
            _caster.Tick(dt);

            if (player == null) return;

            Vector3 pos = player.position;
            Vector3 fwd = player.forward;
            if (fwd.sqrMagnitude < 1e-6f)
                fwd = Vector3.forward;
            else
                fwd.Normalize();

            var ctx = new SkillCastContext
            {
                CasterEntityId = 0,
                CasterPosition = pos,
                CasterForward = fwd,
                TargetPosition = pos + fwd * 10f,
                HitSystem = _hitSystem,
                OnHit = OnSkillHit
            };

            var kb = Keyboard.current;
            var mouse = Mouse.current;

            bool fireMelee = (kb != null && kb.digit1Key.wasPressedThisFrame)
                             || (mouse != null && mouse.leftButton.wasPressedThisFrame);
            if (fireMelee)
            {
                _playerView?.TriggerAttack();
                if (_caster.TryCast(1, ctx))
                    CaptureGizmo(1, pos, fwd, _defMelee);
            }

            if (kb != null)
            {
                if (kb.digit2Key.wasPressedThisFrame && _caster.TryCast(2, ctx))
                    CaptureGizmo(2, pos, fwd, _defHitscan);
                if (kb.digit3Key.wasPressedThisFrame && _caster.TryCast(3, ctx))
                    CaptureGizmo(3, pos, fwd, _defProjectile);
                if (kb.digit4Key.wasPressedThisFrame && _caster.TryCast(4, ctx))
                    CaptureGizmo(4, pos, fwd, _defMeteor);
                if (kb.digit5Key.wasPressedThisFrame && _caster.TryCast(5, ctx))
                    CaptureGizmo(5, pos, fwd, _defZone);
            }

            if (_gizmoShowTime > 0f)
                _gizmoShowTime -= dt;
        }

        void CaptureGizmo(int skillId, Vector3 origin, Vector3 forward, SkillDefine def)
        {
            _lastSkillId = skillId;
            _gizmoShowTime = CastGizmoDuration;
            _gizmoOrigin = origin;
            _gizmoForward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            _gizmoRange = def.Range;
            _gizmoTarget = origin + _gizmoForward * 10f;

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

        void TryBindPlayer()
        {
            if (player == null)
            {
                var views = FindObjectsOfType<PlayerView>();
                foreach (var v in views)
                {
                    if (v != null && v.IsLocal)
                    {
                        player = v.transform;
                        _playerView = v;
                        break;
                    }
                }
                if (player == null)
                {
                    var go = GameObject.Find("Player_Local");
                    if (go != null)
                    {
                        player = go.transform;
                        _playerView = go.GetComponent<PlayerView>();
                    }
                }
            }
            else if (_playerView == null)
            {
                _playerView = player.GetComponent<PlayerView>()
                              ?? player.GetComponentInParent<PlayerView>();
            }

            if (player == null) return;
            _bound = true;
            if (debugDrawer != null)
                debugDrawer.aoiCenter = player;
        }

        void OnSkillHit(int casterId, HitResult hit, SkillDefine def)
        {
            Debug.Log($"[Hit] Skill={def.Name} Target={hit.TargetEntityId} Dist={hit.Distance:F1} Dmg={def.BaseDamage}");
            if (CombatTargetRegistry.TryGetReceiver(hit.TargetEntityId, out var receiver) && receiver != null)
                receiver.OnHit(hit, def);
        }

        void OnDrawGizmos()
        {
            if (player == null) return;

            if (ShowAlwaysPreview && Application.isPlaying)
            {
                Vector3 origin = player.position;
                Vector3 fwd = player.forward;
                if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
                else fwd.Normalize();

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
