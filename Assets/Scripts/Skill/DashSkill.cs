using UnityEngine;

namespace GameAct.Skill
{
    /// <summary>
    /// 冲刺技能：冷却 + 持续窗口由本类管理。
    /// 实际位移由 ActPlayer.RequestDash 驱动（走 WorldMotor.SlideMove，不穿墙）。
    /// BaseDamage > 0 时，冲刺途中做一次身体 Overlap 结算。
    /// </summary>
    public class DashSkill : SkillBase
    {
        float _elapsed;
        bool _hitDone;

        public DashSkill(SkillDefine define) : base(define) { }

        protected override void OnCastStart()
        {
            _elapsed = 0f;
            _hitDone = false;
        }

        protected override void OnTick(float dt)
        {
            _elapsed += dt;

            if (!_hitDone && Define.BaseDamage > 0f && _elapsed >= Define.ResolveHitDelay())
            {
                _hitDone = true;
                DoBodyHit();
            }

            float duration = Define.Duration > 0f ? Define.Duration : 0.18f;
            if (_elapsed >= duration)
                IsActive = false;
        }

        void DoBodyHit()
        {
            if (_ctx.HitSystem == null || _ctx.OnHit == null) return;

            Vector3 fwd = _ctx.CasterForward.sqrMagnitude > 1e-6f
                ? _ctx.CasterForward.normalized
                : Vector3.forward;
            Vector3 center = _ctx.CasterPosition + fwd * (Define.Range * 0.4f);
            float radius = Mathf.Max(0.6f, Define.Range * 0.5f);

            _hitBuffer.Clear();
            _ctx.HitSystem.OverlapSphere(center, radius, Define.TargetLayers, _hitBuffer, Define.MaxTargets);
            ApplyHits();
        }

        protected override void OnStop()
        {
            _hitDone = true;
        }
    }
}
