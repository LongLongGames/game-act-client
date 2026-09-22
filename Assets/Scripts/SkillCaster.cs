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
                return skill.TryCast(ctx);
            }
            return false;
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