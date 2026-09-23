using System.Collections.Generic;
using UnityEngine;

namespace GameAct.Skill
{
    /// <summary>
    /// Runtime skill instance interface.
    /// Created from SkillDefine, managed by SkillCaster / SkillManager.
    /// </summary>
    public interface ISkill
    {
        int SkillId { get; }
        SkillDefine Define { get; }
        bool IsActive { get; }
        bool IsReady { get; }           // cooldown finished

        /// <summary>Try to start casting. Returns false if on cooldown or invalid.</summary>
        bool TryCast(in SkillCastContext ctx);

        /// <summary>Per-frame tick while active (projectile flight, zone tick, delay countdown...).</summary>
        void Tick(float dt);

        /// <summary>Force stop / cleanup.</summary>
        void Stop();
    }

    /// <summary>
    /// Context passed when casting a skill.
    /// </summary>
    public struct SkillCastContext
    {
        public int CasterEntityId;
        public Vector3 CasterPosition;
        public Vector3 CasterForward;
        public Vector3? TargetPosition;     // optional ground target / aim point
        public int? TargetEntityId;         // optional locked target
        public IHitSystem HitSystem;
        public System.Action<int, HitResult, SkillDefine> OnHit;  // callback: targetId, hit, skillDef

        /// <summary>
        /// 本次施法击退距离（米）。由 SkillCaster 在构建 ctx 时写入：
        /// skill.KnockbackDistance ≥ 0 用技能值，否则用 Caster.KnockbackDistance。
        /// OnHit 侧把该值传给 HitReceiver.ApplyKnockback。
        /// 后期与 Monster 体重挂钩时，此值仍是「输出端」；目标端再乘抗性（Boss/大型 = 0）。
        /// </summary>
        public float KnockbackDistance;
    }
}
