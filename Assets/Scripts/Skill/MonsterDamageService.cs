using UnityEngine;

namespace GameAct.Skill
{
    /// <summary>
    /// 联机 Client 的「怪物受击上报」桥（与 MonsterDeathService / MonsterKnockbackService 同模式）。
    /// Client 连上 Host 后由 LesNetworkHub 注册；Solo / Host 不注册，HitReceiver 走本地结算。
    /// 怪物 HP / 死亡只由 Host 结算，Client 本地不再自己判死。
    /// </summary>
    public static class MonsterDamageService
    {
        /// <summary>参数：entityId, damage。</summary>
        public static System.Action<int, float> ClientDamageSender;

        /// <summary>参数：entityId, worldDir(XZ), distance。</summary>
        public static System.Action<int, Vector3, float> ClientKnockbackSender;

        /// <summary>true = 当前是联机 Client，怪物受击需上报 Host。</summary>
        public static bool IsClientProxy => ClientDamageSender != null;

        public static void ReportDamage(int entityId, float damage)
        {
            if (entityId < 0 || damage <= 0f) return;
            ClientDamageSender?.Invoke(entityId, damage);
        }

        public static void ReportKnockback(int entityId, Vector3 worldDir, float distance)
        {
            if (entityId < 0 || distance <= 0.001f) return;
            ClientKnockbackSender?.Invoke(entityId, worldDir, distance);
        }

        public static void Clear()
        {
            ClientDamageSender = null;
            ClientKnockbackSender = null;
        }
    }
}
