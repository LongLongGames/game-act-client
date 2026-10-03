using System;

namespace GameAct.Les.Shared
{
    /// <summary>
    /// 非托管输入，供 HumanControllerLogic 压缩同步。
    /// 增加 Rotation（Yaw），与 UnityExample 一致：鼠标控制朝向，移动相对朝向。
    /// </summary>
    [Serializable]
    public struct ActPlayerInput
    {
        public float MoveX;     // 本地左右（A/D），相对 Yaw
        public float MoveY;     // 本地前后（W/S），相对 Yaw
        public float Rotation;  // Yaw 度数（鼠标累积）
        public byte Flags;      // 1=sprint, 2=jump pressed, 4=attack face yaw valid

        /// <summary>
        /// 平A 序号（电平，每次出拳 +1，溢出回绕）。
        /// 用「序号变化」而非单帧按键，30Hz 逻辑 tick / 丢包 / 输入重发都不会漏出拳。
        /// </summary>
        public byte AttackSeq;
        /// <summary>本次出拳索敌后身体应朝向的 Yaw（仅 AttackFace 为 true 时有效）。</summary>
        public float AttackYaw;

        public bool Sprint => (Flags & 1) != 0;
        public bool Jump => (Flags & 2) != 0;
        public bool AttackFace => (Flags & 4) != 0;

        public static ActPlayerInput FromAxes(float x, float y, float rotation, bool sprint, bool jump)
        {
            byte f = 0;
            if (sprint) f |= 1;
            if (jump) f |= 2;
            return new ActPlayerInput
            {
                MoveX = x,
                MoveY = y,
                Rotation = rotation,
                Flags = f
            };
        }

        /// <summary>兼容旧调用（无 rotation）。</summary>
        public static ActPlayerInput FromAxes(float x, float y, bool sprint, bool jump)
            => FromAxes(x, y, 0f, sprint, jump);
    }
}
