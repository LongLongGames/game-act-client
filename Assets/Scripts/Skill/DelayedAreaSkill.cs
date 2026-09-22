using UnityEngine;

namespace GameAct.Skill
{
    /// <summary>
    /// Warning → wait CastTime → apply area damage.
    /// </summary>
    public class DelayedAreaSkill : SkillBase
    {
        private Vector3 _center;
        private float _timer;

        public DelayedAreaSkill(SkillDefine define) : base(define) { }

        protected override void OnCastStart()
        {
            _center = _ctx.TargetPosition ?? (_ctx.CasterPosition + _ctx.CasterForward * Define.Range);
            _timer = Define.CastTime;

            // TODO: spawn warning decal / VFX here (client only)
        }

        protected override void OnTick(float dt)
        {
            _timer -= dt;
            if (_timer > 0f) return;

            // Time to hit
            _hitBuffer.Clear();
            _ctx.HitSystem.OverlapSphere(_center, Define.Range, Define.TargetLayers, _hitBuffer, Define.MaxTargets);
            ApplyHits();
            Stop();
        }
    }
}