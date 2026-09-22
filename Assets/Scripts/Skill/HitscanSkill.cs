using UnityEngine;

namespace GameAct.Skill
{
    /// <summary>
    /// Instant ray / line attack.
    /// </summary>
    public class HitscanSkill : SkillBase
    {
        public HitscanSkill(SkillDefine define) : base(define) { }

        protected override void OnCastStart()
        {
            Vector3 origin = _ctx.CasterPosition + Vector3.up * 1.2f; // approximate muzzle height
            Vector3 dir = _ctx.CasterForward;
            if (_ctx.TargetPosition.HasValue)
                dir = (_ctx.TargetPosition.Value - origin).normalized;

            _hitBuffer.Clear();
            _ctx.HitSystem.Raycast(origin, dir, Define.Range, Define.TargetLayers, _hitBuffer, closestOnly: true);

            ApplyHits();
            IsActive = false;
        }

        protected override void OnTick(float dt) { }
    }
}