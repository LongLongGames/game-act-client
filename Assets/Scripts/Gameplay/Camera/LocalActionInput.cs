using UnityEngine;
using GameAct.Les.Shared;

namespace GameAct.Gameplay.Camera
{
    /// <summary>
    /// 本地玩家的「动作事件」输入锁存（与 LocalLookInput 同层）。
    /// PlayerCombatDriver 出拳时登记；ActPlayerController / ActPlayer.ReadLocalInput 每次采样输入时 Stamp 进 ActPlayerInput，
    /// 随输入同步到 Host，再由 ActPlayer 的 SyncVar 广播给所有端的远程 PlayerView。
    /// </summary>
    public static class LocalActionInput
    {
        public static byte AttackSeq { get; private set; }
        public static float AttackYaw { get; private set; }
        public static bool AttackHasFace { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Reset();

        /// <summary>开局调用（GameplayRunner.StartSession）。</summary>
        public static void Begin() => Reset();

        /// <summary>退局调用（GameplayRunner.StopSession），避免残留序号让下一局误触发一次出拳。</summary>
        public static void End() => Reset();

        static void Reset()
        {
            AttackSeq = 0;
            AttackYaw = 0f;
            AttackHasFace = false;
        }

        /// <summary>本地平A 成功施放时调用。hasFace=索敌后身体需朝向 faceYawDeg。</summary>
        public static void NotifyMelee(bool hasFace, float faceYawDeg)
        {
            unchecked { AttackSeq++; }
            AttackHasFace = hasFace;
            AttackYaw = faceYawDeg;
        }

        /// <summary>把当前动作锁存写进一帧输入。每一帧输入（含空闲/光标解锁）都必须调用，保证序号是电平。</summary>
        public static void Stamp(ref ActPlayerInput cmd)
        {
            cmd.AttackSeq = AttackSeq;
            cmd.AttackYaw = AttackYaw;
            if (AttackHasFace)
                cmd.Flags |= 4;
        }
    }
}
