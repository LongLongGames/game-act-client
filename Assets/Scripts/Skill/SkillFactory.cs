namespace GameAct.Skill
{
    /// <summary>
    /// Creates concrete skill instances from SkillDefine.
    /// </summary>
    public static class SkillFactory
    {
        public static ISkill Create(SkillDefine define)
        {
            switch (define.ExecType)
            {
                case SkillExecType.Melee:
                    return new MeleeSkill(define);
                case SkillExecType.Hitscan:
                    return new HitscanSkill(define);
                case SkillExecType.Projectile:
                    return new ProjectileSkill(define);
                case SkillExecType.DelayedArea:
                    return new DelayedAreaSkill(define);
                case SkillExecType.PersistentZone:
                    return new PersistentZoneSkill(define);
                case SkillExecType.ShapeArea:
                    // For now reuse melee/fan path; can specialize later
                    return new MeleeSkill(define);
                default:
                    return new MeleeSkill(define);
            }
        }
    }
}