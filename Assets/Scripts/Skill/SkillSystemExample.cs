// Assets/Scripts/Skill/SkillSystemExample.cs
// 仅负责：玩家技能输入 + 命中 CombatTargetRegistry（LES 怪 / Debug 刷怪）。
// 已删除：自动刷 Dummy（请用 F12 → Combat/spawn_monster）。
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

        ISpatialIndex _spatial;
        IHitSystem _hitSystem;
        SkillCaster _caster;
        bool _bound;
        PlayerView _playerView;

        float _lastMeleeRange;
        Vector3 _lastMeleeCenter;
        Vector3 _lastMeleeForward;
        float _lastMeleeRadius;
        float _gizmoShowTime;

        void Start()
        {
            if (debugDrawer == null)
                debugDrawer = FindObjectOfType<SpatialDebugDrawer>();

            _spatial = new SpatialHash(10f);
            var hit = new HitSystem(_spatial);
            hit.GetEntityBounds = id => CombatTargetRegistry.GetBounds(id);
            _hitSystem = hit;

            _caster = new SkillCaster();
            _caster.AddSkill(SkillDefine.CreateMelee("Slash", 2.8f, 15f));
            _caster.AddSkill(SkillDefine.CreateHitscan("Rail", 35f, 30f));
            _caster.AddSkill(SkillDefine.CreateProjectile("Fireball", 16f, 22f));
            _caster.AddSkill(SkillDefine.CreateDelayedArea("Meteor", 4.5f, 1.0f, 50f));
            _caster.AddSkill(SkillDefine.CreatePersistentZone("FireZone", 3.5f, 4f, 8f));

            if (debugDrawer != null)
                debugDrawer.SetSpatialIndex(_spatial);

            Debug.Log("[SkillSystemExample] Ready. Targets=CombatTargetRegistry. Spawn via F12 spawn_monster.");
        }

        void Update()
        {
            if (!_bound)
            {
                TryBindPlayer();
                if (!_bound) return;
            }

            // 施法前把 Registry 同步进 Spatial（含 LES 怪位移） Debug 刷的怪）
            CombatTargetRegistry.SyncToSpatial(_spatial);

            float dt = Time.deltaTime;
            _caster.Tick(dt);

            if (player == null) return;

            var ctx = new SkillCastContext
            {
                CasterEntityId = 0,
                CasterPosition = player.position,
                CasterForward = player.forward,
                TargetPosition = player.position + player.forward * 10f,
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
                {
                    var def = SkillDefine.CreateMelee("Slash", 2.8f, 15f);
                    _lastMeleeRange = def.Range;
                    _lastMeleeCenter = player.position + player.forward.normalized * (def.Range * 0.5f);
                    _lastMeleeForward = player.forward;
                    _lastMeleeRadius = def.Range * 0.6f;
                    _gizmoShowTime = 0.6f;
                }
            }

            if (kb == null) return;

            if (kb.digit2Key.wasPressedThisFrame) _caster.TryCast(2, ctx);
            if (kb.digit3Key.wasPressedThisFrame) _caster.TryCast(3, ctx);
            if (kb.digit4Key.wasPressedThisFrame) _caster.TryCast(4, ctx);
            if (kb.digit5Key.wasPressedThisFrame) _caster.TryCast(5, ctx);

            if (_gizmoShowTime > 0f)
                _gizmoShowTime -= dt;
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

            Debug.Log($"[SkillSystemExample] Bound to {player.name}");
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

            if (_gizmoShowTime > 0f)
            {
                Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.35f);
                Gizmos.DrawSphere(_lastMeleeCenter, _lastMeleeRadius);
                Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.9f);
                Gizmos.DrawWireSphere(_lastMeleeCenter, _lastMeleeRadius);
                Gizmos.DrawLine(player.position, player.position + _lastMeleeForward * _lastMeleeRange);
            }

            if (Application.isPlaying)
            {
                float range = 2.8f;
                Vector3 center = player.position + player.forward.normalized * (range * 0.5f);
                Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.15f);
                Gizmos.DrawSphere(center, range * 0.6f);
                Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
                Gizmos.DrawWireSphere(center, range * 0.6f);
            }
        }
    }
}
