using UnityEngine;
using GameAct.Spatial;

namespace GameAct.Skill
{
    /// <summary>
    /// High-level skill execution type.
    /// Determines which detection path HitSystem will take.
    /// </summary>
    public enum SkillExecType
    {
        /// <summary>Melee / short-range instant (overlap or short ray)</summary>
        Melee,

        /// <summary>Instant hitscan (ray / line)</summary>
        Hitscan,

        /// <summary>Projectile that flies and collides over time</summary>
        Projectile,

        /// <summary>Delay then apply hit in an area (warning circle → damage)</summary>
        DelayedArea,

        /// <summary>Persistent ground zone that ticks damage</summary>
        PersistentZone,

        /// <summary>Fan / cone / ring shaped instant area</summary>
        ShapeArea
    }

    /// <summary>
    /// Shape used for area skills.
    /// </summary>
    public enum SkillShape
    {
        Sphere,
        Box,
        Capsule,
        Fan,        // angle + radius
        Ring        // inner + outer radius
    }

    /// <summary>
    /// Static definition of a skill (usually from config / ScriptableObject / table).
    /// Runtime instances hold this + current state.
    /// </summary>
    [System.Serializable]
    public class SkillDefine
    {
        public int SkillId;
        public string Name;
        public SkillExecType ExecType;

        [Header("Common")]
        public float Cooldown = 1f;
        public float CastTime;              // wind-up before hit
        public float Duration;              // for persistent / projectile lifetime
        public int MaxTargets = 8;          // 0 = unlimited

        [Header("Range / Shape")]
        public float Range = 5f;            // melee range / hitscan max dist / sphere radius
        public float Width = 1f;            // for box / capsule
        public float Angle = 90f;           // for fan
        public float InnerRadius;           // for ring
        public SkillShape Shape = SkillShape.Sphere;

        [Header("Projectile")]
        public float ProjectileSpeed = 20f;
        public float ProjectileRadius = 0.3f;
        public bool ProjectileHoming;

        [Header("Damage (placeholder)")]
        public float BaseDamage = 10f;
        public float DamageInterval = 0.5f; // for persistent zone tick

        [Header("Layers")]
        public SpatialLayer TargetLayers = SpatialLayer.Ground | SpatialLayer.LowAerial;

        // Factory helpers for quick testing
        public static SkillDefine CreateMelee(string name, float range = 2.5f, float damage = 15f)
        {
            return new SkillDefine
            {
                SkillId = 1,
                Name = name,
                ExecType = SkillExecType.Melee,
                Range = range,
                BaseDamage = damage,
                MaxTargets = 3,
                Cooldown = 0.6f
            };
        }

        public static SkillDefine CreateHitscan(string name, float range = 30f, float damage = 20f)
        {
            return new SkillDefine
            {
                SkillId = 2,
                Name = name,
                ExecType = SkillExecType.Hitscan,
                Range = range,
                BaseDamage = damage,
                MaxTargets = 1,
                Cooldown = 0.8f
            };
        }

        public static SkillDefine CreateProjectile(string name, float speed = 18f, float damage = 25f)
        {
            return new SkillDefine
            {
                SkillId = 3,
                Name = name,
                ExecType = SkillExecType.Projectile,
                ProjectileSpeed = speed,
                ProjectileRadius = 0.35f,
                Range = 40f,               // max travel
                BaseDamage = damage,
                Duration = 3f,
                Cooldown = 1.2f
            };
        }

        public static SkillDefine CreateDelayedArea(string name, float radius = 4f, float delay = 1.2f, float damage = 40f)
        {
            return new SkillDefine
            {
                SkillId = 4,
                Name = name,
                ExecType = SkillExecType.DelayedArea,
                Range = radius,
                CastTime = delay,
                BaseDamage = damage,
                Shape = SkillShape.Sphere,
                Cooldown = 5f
            };
        }

        public static SkillDefine CreatePersistentZone(string name, float radius = 3.5f, float duration = 5f, float tickDamage = 8f)
        {
            return new SkillDefine
            {
                SkillId = 5,
                Name = name,
                ExecType = SkillExecType.PersistentZone,
                Range = radius,
                Duration = duration,
                DamageInterval = 0.5f,
                BaseDamage = tickDamage,
                Shape = SkillShape.Sphere,
                Cooldown = 8f
            };
        }
    }
}