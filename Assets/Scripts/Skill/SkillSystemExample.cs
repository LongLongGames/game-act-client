// Assets/Scripts/Skill/SkillSystemExample.cs
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

        [Header("Dummy 生成")]
        public int DummyCount = 12;
        public float SpawnRadius = 18f;
        public GameObject DummyPrefab;          // 可拖 Dummy.prefab，空则运行时创建

        private ISpatialIndex _spatial;
        private IHitSystem _hitSystem;
        private SkillCaster _caster;
        private readonly Dictionary<int, AABB> _entityBounds = new Dictionary<int, AABB>();
        private readonly Dictionary<int, HitReceiver> _hitReceivers = new Dictionary<int, HitReceiver>();
        private readonly List<GameObject> _spawnedDummies = new List<GameObject>();
        private bool _bound;
        private PlayerView _playerView;

        // 攻击范围可视化缓存
        private float _lastMeleeRange;
        private Vector3 _lastMeleeCenter;
        private Vector3 _lastMeleeForward;
        private float _lastMeleeRadius;
        private float _gizmoShowTime;

        private void Start()
        {
            if (debugDrawer == null)
                debugDrawer = FindObjectOfType<SpatialDebugDrawer>();

            _spatial = new SpatialHash(10f);

            var hit = new HitSystem(_spatial);
            hit.GetEntityBounds = id => _entityBounds.TryGetValue(id, out var b) ? b : (AABB?)null;
            _hitSystem = hit;

            // ========== ② 去掉纯静态 Gizmos，改成可见 Dummy ==========
            SpawnDummies();

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

        private void SpawnDummies()
        {
            if (DummyPrefab == null)
            {
                Debug.LogError("[SkillSystemExample] DummyPrefab 未赋值！请把 Assets/Bundles/Character/Monster/Dummy.prefab 拖进来");
                return;
            }

            for (int i = 1; i <= DummyCount; i++)
            {
                Vector3 pos = new Vector3(
                    Random.Range(-SpawnRadius, SpawnRadius),
                    0f,
                    Random.Range(5f, SpawnRadius + 8f)
                );

                GameObject go = Instantiate(DummyPrefab, pos, Quaternion.identity);
                go.name = $"Dummy_{i}";

                // 确保 LogicCollider
                var authoring = go.GetComponent<LogicColliderAuthoring>();
                if (authoring == null)
                {
                    authoring = go.AddComponent<LogicColliderAuthoring>();
                    authoring.Colliders = new[] { LogicColliderData.DefaultBody() };
                }
                var logicCol = authoring.BuildRuntimeCollider();

                // HitReceiver
                var receiver = go.GetComponent<HitReceiver>();
                if (receiver == null)
                    receiver = go.AddComponent<HitReceiver>();
                receiver.EntityId = i;
                receiver.MaxHp = 80f + Random.Range(0, 40);
                receiver.CurrentHp = receiver.MaxHp;

                AABB bounds = logicCol.GetWorldBounds();
                _spatial.Insert(i, bounds, SpatialLayer.Ground);
                _entityBounds[i] = bounds;
                _hitReceivers[i] = receiver;

                if (debugDrawer != null)
                    debugDrawer.SetEntityBounds(i, bounds);

                _spawnedDummies.Add(go);
            }
        }

        private void Update()
        {
            if (!_bound)
            {
                TryBindPlayer();
                if (!_bound) return;
            }

            float dt = Time.deltaTime;
            _caster.Tick(dt);

            // 同步 Dummy 位置到 Spatial（如果以后会移动）
            // 这里 Dummy 静止，可省略

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
                    // 记录攻击范围用于 Gizmos
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

            // 真正把伤害打到 HitReceiver 上
            if (_hitReceivers.TryGetValue(hit.TargetEntityId, out var receiver) && receiver != null)
            {
                receiver.OnHit(hit, def);
            }
        }

        // ========== ① 绘制攻击范围 Gizmos ==========
        private void OnDrawGizmos()
        {
            if (player == null) return;

            // 近战攻击范围（临时显示）
            if (_gizmoShowTime > 0f)
            {
                Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.35f);
                Gizmos.DrawSphere(_lastMeleeCenter, _lastMeleeRadius);

                Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.9f);
                Gizmos.DrawWireSphere(_lastMeleeCenter, _lastMeleeRadius);

                // 方向指示
                Gizmos.DrawLine(player.position, player.position + _lastMeleeForward * _lastMeleeRange);
            }

            // 常驻：当前玩家面前的近战预览范围（半透明）
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

        private void OnDestroy()
        {
            foreach (var go in _spawnedDummies)
            {
                if (go != null) Destroy(go);
            }
            _spawnedDummies.Clear();
        }
    }
}