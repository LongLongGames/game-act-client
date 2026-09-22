using System.Collections.Generic;
using UnityEngine;

namespace GameAct.Skill
{
    /// <summary>
    /// Base class for concrete skills. Handles cooldown and common state.
    /// </summary>
    public abstract class SkillBase : ISkill
    {
        public int SkillId => Define.SkillId;
        public SkillDefine Define { get; private set; }
        public bool IsActive { get; protected set; }
        public bool IsReady => _cooldownLeft <= 0f;

        protected float _cooldownLeft;
        protected SkillCastContext _ctx;
        protected readonly List<HitResult> _hitBuffer = new List<HitResult>(16);

        public SkillBase(SkillDefine define)
        {
            Define = define;
        }

        public virtual bool TryCast(in SkillCastContext ctx)
        {
            if (!IsReady || IsActive) return false;

            _ctx = ctx;
            IsActive = true;
            _cooldownLeft = Define.Cooldown;
            OnCastStart();
            return true;
        }

        public virtual void Tick(float dt)
        {
            if (_cooldownLeft > 0f)
                _cooldownLeft -= dt;

            if (IsActive)
                OnTick(dt);
        }

        public virtual void Stop()
        {
            if (!IsActive) return;
            IsActive = false;
            OnStop();
        }

        protected abstract void OnCastStart();
        protected abstract void OnTick(float dt);
        protected virtual void OnStop() { }

        protected void ApplyHits()
        {
            if (_ctx.OnHit == null) return;
            foreach (var hit in _hitBuffer)
            {
                _ctx.OnHit(_ctx.CasterEntityId, hit, Define);
            }
        }
    }
}