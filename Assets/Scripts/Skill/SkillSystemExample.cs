using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;   // ← 加这行
using GameAct.Spatial;

namespace GameAct.Skill
{
    /// <summary>
    /// Minimal integration example.
    /// Demonstrates: Spatial + HitSystem + SkillCaster + a few skill types.
    /// </summary>
    public class SkillSystemExample : MonoBehaviour
    {
        [Header("Refs")]
        public Transform player;
        public SpatialDebugDrawer debugDrawer;

        private ISpatialIndex _spatial;
        private IHitSystem _hitSystem;
        private SkillCaster _caster;
        private readonly Dictionary<int, AABB> _entityBounds = new Dictionary<int, AABB>();

        private void Start()
        {
            // 1. Spatial
            _spatial = new SpatialHash(10f);

            // 2. HitSystem wired to Spatial
            var hit = new HitSystem(_spatial);
            hit.GetEntityBounds = id => _entityBounds.TryGetValue(id, out var b) ? b : (AABB?)null;
            _hitSystem = hit;

            // 3. Dummy targets
            for (int i = 1; i <= 20; i++)
            {
                Vector3 pos = new Vector3(Random.Range(-15f, 15f), 0f, Random.Range(5f, 25f));
                var bounds = AABB.FromCenterSize(pos, new Vector3(1f, 2f, 1f));
                _spatial.Insert(i, bounds, SpatialLayer.Ground);
                _entityBounds[i] = bounds;

                if (debugDrawer != null)
                    debugDrawer.SetEntityBounds(i, bounds);
            }

            // 4. Player skills
            _caster = new SkillCaster();
            _caster.AddSkill(SkillDefine.CreateMelee("Slash", 2.8f, 15f));
            _caster.AddSkill(SkillDefine.CreateHitscan("Rail", 35f, 30f));
            _caster.AddSkill(SkillDefine.CreateProjectile("Fireball", 16f, 22f));
            _caster.AddSkill(SkillDefine.CreateDelayedArea("Meteor", 4.5f, 1.0f, 50f));
            _caster.AddSkill(SkillDefine.CreatePersistentZone("FireZone", 3.5f, 4f, 8f));

            if (debugDrawer != null)
            {
                debugDrawer.SetSpatialIndex(_spatial);
                debugDrawer.aoiCenter = player != null ? player : transform;
            }

            Debug.Log("[SkillSystemExample] Ready. Press 1~5 to cast skills.");
        }

        private void Update()
        {
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
            if (kb == null) return;

            if (kb.digit1Key.wasPressedThisFrame) _caster.TryCast(1, ctx); // Melee
            if (kb.digit2Key.wasPressedThisFrame) _caster.TryCast(2, ctx); // Hitscan
            if (kb.digit3Key.wasPressedThisFrame) _caster.TryCast(3, ctx); // Projectile
            if (kb.digit4Key.wasPressedThisFrame) _caster.TryCast(4, ctx); // Delayed
            if (kb.digit5Key.wasPressedThisFrame) _caster.TryCast(5, ctx); // Zone
        }

        private void OnSkillHit(int casterId, HitResult hit, SkillDefine def)
        {
            Debug.Log($"[Hit] Skill={def.Name} Target={hit.TargetEntityId} Dist={hit.Distance:F1} Dmg={def.BaseDamage}");
        }
    }
}