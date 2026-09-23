namespace GameAct.Skill
{
    /// <summary>
    /// View 层死亡 → 权威销毁 ActMonster 的桥（Solo/Host 在 Session 启动时注册）。
    /// </summary>
    public static class MonsterDeathService
    {
        /// <summary>参数：LES 实体 Id（与 MonsterView.EntityId / HitReceiver.EntityId 一致）。</summary>
        public static System.Action<int> AuthorityDestroyMonster;

        public static void RequestDestroy(int entityId)
        {
            if (entityId < 0) return;
            AuthorityDestroyMonster?.Invoke(entityId);
        }

        public static void Clear()
        {
            AuthorityDestroyMonster = null;
        }
    }
}
