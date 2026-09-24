using UnityEngine;

namespace GameAct.Skill
{
    /// <summary>
    /// Flying projectile. Kinematic movement + sphere overlap each tick.
    /// Visual: loads prefab from Bundles/VFX (Fireball / generic Projectile) and follows position.
    /// For full LES integration later: spawn as PredictedEntity.
    /// </summary>
    public class ProjectileSkill : SkillBase
    {
        private Vector3 _position;
        private Vector3 _velocity;
        private float _aliveTime;

        // Visual instance (from Bundles/VFX)
        private GameObject _vfxInstance;
        private Transform _vfxTransform;
        private ParticleSystem[] _particleSystems;

        // Fallback when no prefab found
        private static GameObject _fallbackSpherePrefab;

        public ProjectileSkill(SkillDefine define) : base(define) { }

        protected override void OnCastStart()
        {
            _position = _ctx.CasterPosition + Vector3.up * 1.2f + _ctx.CasterForward * 0.8f;
            Vector3 dir = _ctx.CasterForward;
            if (_ctx.TargetPosition.HasValue)
            {
                Vector3 toTarget = _ctx.TargetPosition.Value - _position;
                if (toTarget.sqrMagnitude > 1e-6f)
                    dir = toTarget.normalized;
            }

            _velocity = dir.normalized * Define.ProjectileSpeed;
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

            // Position update (kinematic)
            _position += _velocity * dt;

            // Keep visual in sync every frame
            SyncVfxPosition();

            // Simple collision check
            _hitBuffer.Clear();
            _ctx.HitSystem.OverlapSphere(_position, Define.ProjectileRadius, Define.TargetLayers, _hitBuffer, Define.MaxTargets);

            if (_hitBuffer.Count > 0)
            {
                ApplyHits();
                PlayImpactVfx();
                Stop(); // destroy on first hit (can change to pierce later)
            }
        }

        protected override void OnStop()
        {
            DestroyVfx();
        }

        // -------------------------------------------------------------------------
        // VFX helpers – prefer Bundles/VFX resources
        // -------------------------------------------------------------------------

        private void SpawnVfx()
        {
            DestroyVfx();

            GameObject prefab = TryLoadVfxPrefab();
            if (prefab != null)
            {
                _vfxInstance = Object.Instantiate(prefab, _position, Quaternion.LookRotation(_velocity.normalized));
            }
            else
            {
                // Runtime fallback so fireball is still visible without art
                _vfxInstance = CreateFallbackSphere();
                _vfxInstance.transform.position = _position;
                _vfxInstance.transform.rotation = Quaternion.LookRotation(_velocity.normalized);
            }

            _vfxTransform = _vfxInstance.transform;
            _particleSystems = _vfxInstance.GetComponentsInChildren<ParticleSystem>(true);

            // Scale roughly to projectile radius
            float s = Mathf.Max(0.4f, Define.ProjectileRadius * 2.2f);
            _vfxTransform.localScale = Vector3.one * s;
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
            // Optional: if the prefab has a child named "Impact", enable it briefly.
            // For now just stop emission so the trail dies cleanly.
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
        /// Load order:
        /// 1. Resources path "VFX/Fireball" or "VFX/Projectile" (if you put assets under Resources)
        /// 2. Common AssetBundle path convention: Bundles/VFX/... (via AssetBundleFramework if present)
        /// 3. null → fallback sphere
        /// </summary>
        private GameObject TryLoadVfxPrefab()
        {
            // Prefer named fireball when skill name contains fire/ball
            string[] candidates =
            {
                "VFX/Fireball",
                "VFX/Projectile_Fireball",
                "VFX/Projectile",
                "Bundles/VFX/Fireball",
                "Bundles/VFX/Projectile"
            };

            foreach (var path in candidates)
            {
                var go = Resources.Load<GameObject>(path);
                if (go != null)
                    return go;
            }

            // If project uses AssetBundleFramework, try a soft reflection load
            // (avoids hard dependency when framework is not yet linked)
            try
            {
                var type = System.Type.GetType("AssetBundleFramework.AssetManager, Assembly-CSharp")
                           ?? System.Type.GetType("AssetBundleFramework.AssetManager");
                if (type != null)
                {
                    var method = type.GetMethod("LoadAsset", new[] { typeof(string), typeof(System.Type) })
                                 ?? type.GetMethod("Load", new[] { typeof(string) });
                    if (method != null)
                    {
                        object instance = null;
                        var prop = type.GetProperty("Instance") ?? type.GetProperty("Current");
                        if (prop != null)
                            instance = prop.GetValue(null);

                        foreach (var path in new[] { "VFX/Fireball", "VFX/Projectile", "Bundles/VFX/Fireball" })
                        {
                            object result = method.IsStatic
                                ? method.Invoke(null, method.GetParameters().Length == 2
                                    ? new object[] { path, typeof(GameObject) }
                                    : new object[] { path })
                                : method.Invoke(instance, method.GetParameters().Length == 2
                                    ? new object[] { path, typeof(GameObject) }
                                    : new object[] { path });

                            if (result is GameObject go)
                                return go;
                        }
                    }
                }
            }
            catch
            {
                // ignore – fallback will be used
            }

            return null;
        }

        private static GameObject CreateFallbackSphere()
        {
            if (_fallbackSpherePrefab == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "FireballFallback";
                Object.Destroy(go.GetComponent<Collider>());
                var r = go.GetComponent<Renderer>();
                if (r != null)
                {
                    // Unlit-ish orange so it is obvious
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                           ?? Shader.Find("Standard")
                                           ?? Shader.Find("Sprites/Default"));
                    if (mat != null)
                    {
                        mat.color = new Color(1f, 0.45f, 0.08f, 1f);
                        if (mat.HasProperty("_EmissionColor"))
                        {
                            mat.EnableKeyword("_EMISSION");
                            mat.SetColor("_EmissionColor", new Color(1f, 0.35f, 0.05f) * 2.5f);
                        }
                        r.sharedMaterial = mat;
                    }
                }
                go.SetActive(false);
                Object.DontDestroyOnLoad(go);
                _fallbackSpherePrefab = go;
            }

            var instance = Object.Instantiate(_fallbackSpherePrefab);
            instance.SetActive(true);
            return instance;
        }
    }
}
