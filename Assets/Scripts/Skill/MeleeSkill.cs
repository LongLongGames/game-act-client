using System.Collections.Generic;
using UnityEngine;

namespace GameAct.Skill
{
    /// <summary>
    /// 近战：前摇到 HitDelay 后开启命中盒，持续 HitWindow，期间对目标各结算一次。
    /// 时间来自 SkillDefine（帧或秒，兼容导表）。
    /// </summary>
    public class MeleeSkill : SkillBase
    {
        float _elapsed;
        bool _windowOpened;
        bool _finished;
        readonly HashSet<int> _hitOnce = new HashSet<int>(8);

        public MeleeSkill(SkillDefine define) : base(define) { }

        protected override void OnCastStart()
        {
            _elapsed = 0f;
            _windowOpened = false;
            _finished = false;
            _hitOnce.Clear();
            // 保持 IsActive，由 OnTick 在窗口结束后关掉
        }

        protected override void OnTick(float dt)
        {
            if (_finished) return;

            _elapsed += dt;
            float delay = Define.ResolveHitDelay();
            float window = Define.ResolveHitWindow();

            if (_elapsed < delay)
                return;

            // 进入命中窗：做一次（或窗口内首次）范围判定
            if (!_windowOpened)
            {
                _windowOpened = true;
                DoOverlapAndApply();
            }

            // 窗口结束（window=0 表示只判开启那一瞬）
            if (_elapsed >= delay + window)
            {
                _finished = true;
                IsActive = false;
            }
        }

        void DoOverlapAndApply()
        {
            if (_ctx.HitSystem == null) return;

            Vector3 forward = _ctx.CasterForward.sqrMagnitude > 1e-6f
                ? _ctx.CasterForward.normalized
                : Vector3.forward;
            Vector3 center = _ctx.CasterPosition + forward * (Define.Range * 0.5f);
            float radius = Define.Range * 0.6f;

            _hitBuffer.Clear();
            _ctx.HitSystem.OverlapSphere(center, radius, Define.TargetLayers, _hitBuffer, Define.MaxTargets);

            if (_ctx.OnHit == null) return;
            foreach (var hit in _hitBuffer)
            {
                if (!_hitOnce.Add(hit.TargetEntityId))
                    continue;
                _ctx.OnHit(_ctx.CasterEntityId, hit, Define);
            }
        }

        protected override void OnStop()
        {
            _finished = true;
            _hitOnce.Clear();
        }
    }
}
