using System.Collections.Generic;
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

        private ISpatialIndex _spatial;
        private IHitSystem _hitSystem;
        private SkillCaster _caster;
        private readonly Dictionary<int, AABB> _entityBounds = new Dictionary<int, AABB>();
        private bool _bound;
        private PlayerView _playerView;

        private void Start()
        {
            if (debugDrawer == null)
                debugDrawer = FindObjectOfType<SpatialDebugDrawer>();

            _spatial = new SpatialHash(10f);

            var hit = new HitSystem(_spatial);
            hit.GetEntityBounds = id => _entityBounds.TryGetValue(id, out var b) ? b : (AABB?)null;
            _hitSystem = hit;

            for (int i = 1; i <= 20; i++)
            {
                Vector3 pos = new Vector3(Random.Range(-15f, 15f), 0f, Random.Range(5f, 25f));
                var bounds = AABB.FromCenterSize(pos, new Vector3(1f, 2f, 1f));
                _spatial.Insert(i, bounds, SpatialLayer.Ground);
                _entityBounds[i] = bounds;

                if (debugDrawer != null)
                    debugDrawer.SetEntityBounds(i, bounds);
            }

            _caster = new SkillCaster();
            _caster.AddSkill(SkillDefine.CreateMelee("Slash", 2.8f, 15f));
            _caster.AddSkill(SkillDefine.CreateHitscan("Rail", 35f, 30f));
            _caster.AddSkill(SkillDefine.CreateProjectile("Fireball", 16f, 22f));
            _caster.AddSkill(SkillDefine.CreateDelayedArea("Meteor", 4.5f, 1.0f, 50f));
            _caster.AddSkill(SkillDefine.CreatePersistentZone("FireZone", 3.5f, 4f, 8f));

            if (debugDrawer != null)
                debugDrawer.SetSpatialIndex(_spatial);

            Debug.Log("[SkillSystemExample] Ready. Waiting for Player_Local...");
        }

        private void Update()
        {
            // 等 GameplayRunner 把 PlayerView 实例化出来再绑
            if (!_bound)
            {
                TryBindPlayer();
                if (!_bound) return;
            }

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

            // 左键 / 数字1：近战 + 攻击动画 Trigger
            bool fireMelee = (kb != null && kb.digit1Key.wasPressedThisFrame)
                             || (mouse != null && mouse.leftButton.wasPressedThisFrame);
            if (fireMelee)
            {
                _playerView?.TriggerAttack();
                _caster.TryCast(1, ctx);
            }

            if (kb == null) return;

            if (kb.digit2Key.wasPressedThisFrame) _caster.TryCast(2, ctx);
            if (kb.digit3Key.wasPressedThisFrame) _caster.TryCast(3, ctx);
            if (kb.digit4Key.wasPressedThisFrame) _caster.TryCast(4, ctx);
            if (kb.digit5Key.wasPressedThisFrame) _caster.TryCast(5, ctx);
        }

        private void TryBindPlayer()
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

        private void OnSkillHit(int casterId, HitResult hit, SkillDefine def)
        {
            Debug.Log($"[Hit] Skill={def.Name} Target={hit.TargetEntityId} Dist={hit.Distance:F1} Dmg={def.BaseDamage}");
        }
    }
}
