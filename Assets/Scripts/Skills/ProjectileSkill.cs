using UnityEngine;

namespace GameAct.Skill
{
    /// <summary>
    /// Flying projectile. Simple kinematic movement + sphere overlap each tick.
    /// For full LES integration later: spawn as PredictedEntity.
    /// </summary>
    public class ProjectileSkill : SkillBase
    {
        private Vector3 _position;
        private Vector3 _velocity;
        private float _aliveTime;

        public ProjectileSkill(SkillDefine define) : base(define) { }

        protected override void OnCastStart()
        {
            _position = _ctx.CasterPosition + Vector3.up * 1.2f + _ctx.CasterForward * 0.8f;
            Vector3 dir = _ctx.CasterForward;
            if (_ctx.TargetPosition.HasValue)
                dir = (_ctx.TargetPosition.Value - _position).normalized;

            _velocity = dir.normalized * Define.ProjectileSpeed;
            _aliveTime = 0f;
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

            // Simple collision check
            _hitBuffer.Clear();
            _ctx.HitSystem.OverlapSphere(_position, Define.ProjectileRadius, Define.TargetLayers, _hitBuffer, Define.MaxTargets);

            if (_hitBuffer.Count > 0)
            {
                ApplyHits();
                Stop(); // destroy on first hit (can change to pierce later)
            }
        }
    }
}