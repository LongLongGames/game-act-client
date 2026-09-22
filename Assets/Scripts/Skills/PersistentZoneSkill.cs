using UnityEngine;

namespace GameAct.Skill
{
    /// <summary>
    /// Ground zone that periodically damages enemies inside.
    /// </summary>
    public class PersistentZoneSkill : SkillBase
    {
        private Vector3 _center;
        private float _aliveTime;
        private float _tickTimer;

        public PersistentZoneSkill(SkillDefine define) : base(define) { }

        protected override void OnCastStart()
        {
            _center = _ctx.TargetPosition ?? (_ctx.CasterPosition + _ctx.CasterForward * 2f);
            _aliveTime = 0f;
            _tickTimer = 0f;

            // TODO: spawn persistent VFX
        }

        protected override void OnTick(float dt)
        {
            _aliveTime += dt;
            if (_aliveTime >= Define.Duration)
            {
                Stop();
                return;
            }

            _tickTimer += dt;
            if (_tickTimer >= Define.DamageInterval)
            {
                _tickTimer -= Define.DamageInterval;

                _hitBuffer.Clear();
                _ctx.HitSystem.OverlapSphere(_center, Define.Range, Define.TargetLayers, _hitBuffer, Define.MaxTargets);
                ApplyHits();
            }
        }
    }
}