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
        public byte Flags;      // 1=sprint, 2=jump pressed

        public bool Sprint => (Flags & 1) != 0;
        public bool Jump => (Flags & 2) != 0;

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
