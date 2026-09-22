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
    }
}