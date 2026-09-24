using UnityEngine;

namespace GameAct.Skill
{
    /// <summary>
    /// Flying projectile. Kinematic + sphere overlap.
    /// VFX：只从 Assets/Bundles/VFX/FireBall 加载，不运行时 CreatePrimitive。
    /// </summary>
    public class ProjectileSkill : SkillBase
    {
        // 与 CharacterPrefabLoader 同一约定
        public const string VfxResourcePath = "VFX/FireBall";
        public const string VfxEditorPath = "Assets/Bundles/VFX/FireBall.prefab";

        private Vector3 _position;
        private Vector3 _velocity;
        private float _aliveTime;

        private GameObject _vfxInstance;
        private Transform _vfxTransform;
        private ParticleSystem[] _particleSystems;

        private static GameObject _cachedPrefab;

        public ProjectileSkill(SkillDefine define) : base(define) { }

        protected override void OnCastStart()
        {
            _position = _ctx.CasterPosition + Vector3.up * 1.2f + _ctx.CasterForward * 0.8f;

            Vector3 dir = _ctx.CasterForward.sqrMagnitude > 1e-6f
                ? _ctx.CasterForward.normalized
                : Vector3.forward;

            if (_ctx.TargetPosition.HasValue)
            {
                Vector3 toTarget = _ctx.TargetPosition.Value - _position;
                if (toTarget.sqrMagnitude > 1e-6f)
                    dir = toTarget.normalized;
            }

            _velocity = dir * Define.ProjectileSpeed;
            _aliveTime = 0f;

            SpawnVfx();
            SyncVfxPosition();
        }

        protected override void OnTick(float dt)
        {
            _aliveTime += dt;
            if (_aliveTime >= Define.Duration)
            {
                Stop();
                return;
            }

            _position += _velocity * dt;
            SyncVfxPosition();

            _hitBuffer.Clear();
            _ctx.HitSystem.OverlapSphere(_position, Define.ProjectileRadius, Define.TargetLayers, _hitBuffer, Define.MaxTargets);

            if (_hitBuffer.Count > 0)
            {
                ApplyHits();
                PlayImpactVfx();
                Stop();
            }
        }

        protected override void OnStop()
        {
            DestroyVfx();
        }

        private void SpawnVfx()
        {
            DestroyVfx();

            var prefab = LoadFireBallPrefab();
            if (prefab == null)
            {
                Debug.LogError($"[ProjectileSkill] 未找到 VFX：{VfxEditorPath}（或 Resources/{VfxResourcePath}）。禁止运行时 Create。");
                return;
            }

            Quaternion rot = _velocity.sqrMagnitude > 1e-4f
                ? Quaternion.LookRotation(_velocity.normalized)
                : Quaternion.identity;

            _vfxInstance = Object.Instantiate(prefab, _position, rot);
            _vfxInstance.name = "FireBall_Runtime";
            _vfxTransform = _vfxInstance.transform;
            _particleSystems = _vfxInstance.GetComponentsInChildren<ParticleSystem>(true);

            // 不强制改 scale，保留 prefab 原尺寸；仅当半径异常大时略放大
            if (Define.ProjectileRadius > 0.5f)
            {
                float s = Define.ProjectileRadius * 2f;
                _vfxTransform.localScale = Vector3.one * s;
            }
        }

        private void SyncVfxPosition()
        {
            if (_vfxTransform == null) return;
            _vfxTransform.position = _position;
            if (_velocity.sqrMagnitude > 1e-4f)
                _vfxTransform.rotation = Quaternion.LookRotation(_velocity.normalized);
        }

        private void PlayImpactVfx()
        {
            if (_particleSystems == null) return;
            foreach (var ps in _particleSystems)
            {
                if (ps == null) continue;
                var em = ps.emission;
                em.enabled = false;
            }
        }

        private void DestroyVfx()
        {
            if (_vfxInstance != null)
            {
                Object.Destroy(_vfxInstance);
                _vfxInstance = null;
            }
            _vfxTransform = null;
            _particleSystems = null;
        }

        /// <summary>
        /// 与 CharacterPrefabLoader.Load 相同：先 Resources，Editor 再 AssetDatabase 直读 Bundles。
        /// </summary>
        private static GameObject LoadFireBallPrefab()
        {
            if (_cachedPrefab != null)
                return _cachedPrefab;

            var res = Resources.Load<GameObject>(VfxResourcePath);
            if (res != null)
            {
                _cachedPrefab = res;
                return _cachedPrefab;
            }

#if UNITY_EDITOR
            var adType = System.Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            if (adType != null)
            {
                var method = adType.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(System.Type) });
                if (method != null)
                {
                    var prefab = method.Invoke(null, new object[] { VfxEditorPath, typeof(GameObject) }) as GameObject;
                    if (prefab != null)
                    {
                        _cachedPrefab = prefab;
                        return _cachedPrefab;
                    }
                }
            }
#endif

            // 兼容小写 fireball 文件名
#if UNITY_EDITOR
            if (adType != null)
            {
                var method = adType.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(System.Type) });
                if (method != null)
                {
                    foreach (var alt in new[]
                             {
                                 "Assets/Bundles/VFX/Fireball.prefab",
                                 "Assets/Bundles/VFX/FireBall.prefab"
                             })
                    {
                        var prefab = method.Invoke(null, new object[] { alt, typeof(GameObject) }) as GameObject;
                        if (prefab != null)
                        {
                            _cachedPrefab = prefab;
                            return _cachedPrefab;
                        }
                    }
                }
            }
#endif

            return null;
        }
    }
}
