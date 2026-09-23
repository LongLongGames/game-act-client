using System.Collections.Generic;
using UnityEngine;

namespace GameAct.Skill
{
    /// <summary>
    /// Simple skill holder + updater for one entity (player or monster).
    /// Attach to entity or manage externally.
    /// </summary>
    public class SkillCaster
    {
        private readonly List<ISkill> _skills = new List<ISkill>();
        private readonly Dictionary<int, ISkill> _skillMap = new Dictionary<int, ISkill>();

        public IReadOnlyList<ISkill> Skills => _skills;

        /// <summary>
        /// 默认击退距离（米）。施法时写入 SkillCastContext.KnockbackDistance。
        /// 技能自身 SkillDefine.KnockbackDistance ≥ 0 时优先用技能值；否则用本字段。
        /// 后期可与 Monster 体重挂钩：距离最终 = Caster 输出 × 目标抗性系数，Boss/大型怪系数为 0。
        /// </summary>
        public float KnockbackDistance = 1.2f;

        public void AddSkill(SkillDefine define)
        {
            if (_skillMap.ContainsKey(define.SkillId)) return;

            var skill = SkillFactory.Create(define);
            _skills.Add(skill);
            _skillMap[define.SkillId] = skill;
        }

        public bool TryCast(int skillId, in SkillCastContext ctx)
        {
            if (_skillMap.TryGetValue(skillId, out var skill))
            {
                Debug.Log($"[SkillCaster] TryCast {skill.Define.Name} (id={skillId}) kb={ResolveKnockback(skill.Define):F2}");
                return skill.TryCast(ctx);
            }
            Debug.LogWarning($"[SkillCaster] TryCast failed for skill id={skillId}");
            return false;
        }

        /// <summary>
        /// 解析本次施法实际击退距离：技能配置 ≥0 优先，否则用 Caster.KnockbackDistance。
        /// 负值表示本技能不击退。
        /// </summary>
        public float ResolveKnockback(SkillDefine def)
        {
            if (def != null && def.KnockbackDistance >= 0f)
                return def.KnockbackDistance;
            return KnockbackDistance;
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < _skills.Count; i++)
            {
                _skills[i].Tick(dt);
            }
        }

        public ISkill GetSkill(int skillId)
        {
            _skillMap.TryGetValue(skillId, out var skill);
            return skill;
        }
    }
}
