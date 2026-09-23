using UnityEngine;
using GameAct.Spatial;

namespace GameAct.Skill
{
    public enum SkillExecType
    {
        Melee,
        Hitscan,
        Projectile,
        DelayedArea,
        PersistentZone,
        ShapeArea
    }

    public enum SkillShape
    {
        Sphere,
        Box,
        Capsule,
        Fan,
        Ring
    }

    /// <summary>
    /// 技能静态定义。字段与导表列对齐：可用「秒」覆盖，或用「帧 + AnimFps」换算。
    /// 近战示例（当前攻击动画 36 帧、第 7 帧 hit）：
    ///   AnimFps=30, HitFrame=7, HitWindowFrames=3
    ///   → HitDelay≈0.233s, HitWindow≈0.1s
    /// </summary>
    [System.Serializable]
    public class SkillDefine
    {
        public int SkillId;
        public string Name;
        public SkillExecType ExecType;

        [Header("Common")]
        public float Cooldown = 1f;
        public float CastTime;
        public float Duration;
        public int MaxTargets = 8;

        [Header("Timing / Hit window（表可导：秒 或 帧）")]
        [Tooltip("动画采样帧率，用于 Frame→秒。默认 30。")]
        public float AnimFps = 30f;

        [Tooltip("命中开始帧（从 clip 起点计，与 Animation 窗口帧号一致）。0 表示不用帧、改用 HitDelaySec。")]
        public int HitFrame;

        [Tooltip("命中盒持续帧数。0 且 HitWindowSec≤0 时，命中点只判一次。")]
        public int HitWindowFrames = 3;

        [Tooltip("命中延迟（秒）。>0 时优先于 HitFrame 换算，方便表直接写秒。")]
        public float HitDelaySec;

        [Tooltip("命中盒持续（秒）。>0 时优先于 HitWindowFrames。")]
        public float HitWindowSec;

        [Header("Range / Shape")]
        public float Range = 5f;
        public float Width = 1f;
        public float Angle = 90f;
        public float InnerRadius;
        public SkillShape Shape = SkillShape.Sphere;

        [Header("Projectile")]
        public float ProjectileSpeed = 20f;
        public float ProjectileRadius = 0.3f;
        public bool ProjectileHoming;

        [Header("Damage (placeholder)")]
        public float BaseDamage = 10f;
        public float DamageInterval = 0.5f;

        [Header("Layers")]
        public SpatialLayer TargetLayers = SpatialLayer.Ground | SpatialLayer.LowAerial;

        /// <summary>从施法开始到命中盒开启的秒数。</summary>
        public float ResolveHitDelay()
        {
            if (HitDelaySec > 0f)
                return HitDelaySec;
            if (HitFrame > 0 && AnimFps > 0.01f)
                return HitFrame / AnimFps;
            if (CastTime > 0f)
                return CastTime;
            return 0f;
        }

        /// <summary>命中盒开启后的持续秒数；0 表示仅在开启瞬间判定一次。</summary>
        public float ResolveHitWindow()
        {
            if (HitWindowSec > 0f)
                return HitWindowSec;
            if (HitWindowFrames > 0 && AnimFps > 0.01f)
                return HitWindowFrames / AnimFps;
            return 0f;
        }

        /// <summary>
        /// 近战。默认对齐：36 帧动画、第 7 帧 hit、命中盒约 3 帧（AnimFps=30）。
        /// </summary>
        public static SkillDefine CreateMelee(
            string name,
            float range = 2.5f,
            float damage = 15f,
            int hitFrame = 7,
            int hitWindowFrames = 3,
            float animFps = 30f)
        {
            return new SkillDefine
            {
                SkillId = 1,
                Name = name,
                ExecType = SkillExecType.Melee,
                Range = range,
                BaseDamage = damage,
                MaxTargets = 3,
                Cooldown = 0.6f,
                AnimFps = animFps,
                HitFrame = hitFrame,
                HitWindowFrames = hitWindowFrames
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
                Range = 40f,
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
