using UnityEngine;

namespace GameAct.Gameplay.Simulation
{
    /// <summary>
    /// 一帧输入指令。单机与联机共用同一结构。
    /// </summary>
    public struct PlayerInputCmd
    {
        public Vector2 Move;   // WASD / 左摇杆，-1~1
        public bool Sprint;
        public bool Jump;
        public uint Sequence;  // 联机预测用序号（单机可 0）

        public static PlayerInputCmd None => default;
    }
}
