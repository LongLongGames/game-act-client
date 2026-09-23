using UnityEngine;

namespace GameAct.Skill
{
    /// <summary>
    /// View/技能层 → 权威 ActMonster 击退的桥（与 MonsterDeathService 同模式）。
    /// Solo/Host 在 Session 启动时注册；未注册时仅打日志，避免纯 Client 误改。
    /// </summary>
    public static class MonsterKnockbackService
    {
        /// <summary>
        /// 参数：entityId, worldDir, distance。
        /// 由 LesAuthoritySession / Host 写入，内部改 ActMonster 位置。
        /// </summary>
        public static System.Action<int, Vector3, float> AuthorityKnockback;

        public static void RequestKnockback(int entityId, Vector3 worldDir, float distance)
        {
            if (entityId < 0 || distance <= 0.001f) return;
            if (AuthorityKnockback == null)
            {
                Debug.LogWarning($"[MonsterKnockback] 无权威回调，击退被忽略 id={entityId}（需 Solo/Host 注册）");
                return;
            }
            AuthorityKnockback.Invoke(entityId, worldDir, distance);
        }

        public static void Clear()
        {
            AuthorityKnockback = null;
        }
    }
}
