using System;

namespace GameAct.Les.Shared
{
    /// <summary>非托管输入，供 HumanControllerLogic 压缩同步。</summary>
    [Serializable]
    public struct ActPlayerInput
    {
        public float MoveX;
        public float MoveY;
        public byte Flags; // 1=sprint, 2=jump pressed

        public bool Sprint => (Flags & 1) != 0;
        public bool Jump => (Flags & 2) != 0;

        public static ActPlayerInput FromAxes(float x, float y, bool sprint, bool jump)
        {
            byte f = 0;
            if (sprint) f |= 1;
            if (jump) f |= 2;
            return new ActPlayerInput { MoveX = x, MoveY = y, Flags = f };
        }
    }
}
