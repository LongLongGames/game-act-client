using UnityEngine;

namespace GameAct.Skill
{
    /// <summary>
    /// Instant melee / short-range attack (overlap in front of caster).
    /// </summary>
    public class MeleeSkill : SkillBase
    {
        public MeleeSkill(SkillDefine define) : base(define) { }

        protected override void OnCastStart()
        {
            // Instant hit on cast
            Vector3 center = _ctx.CasterPosition + _ctx.CasterForward.normalized * (Define.Range * 0.5f);
            float radius = Define.Range * 0.6f; // approximate forward sphere

            _hitBuffer.Clear();
            _ctx.HitSystem.OverlapSphere(center, radius, Define.TargetLayers, _hitBuffer, Define.MaxTargets);

            // Optional: also try a short fan for better melee feel
            // _ctx.HitSystem.QueryFan(_ctx.CasterPosition, _ctx.CasterForward, 120f, Define.Range, Define.TargetLayers, _hitBuffer, Define.MaxTargets);

            ApplyHits();
            IsActive = false; // melee finishes immediately
        }

        protected override void OnTick(float dt) { }
    }
}