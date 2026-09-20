using UnityEngine;

namespace GameAct.Gameplay.Simulation
{
    /// <summary>
    /// 玩法模拟统一接口。单机 / Host / Client 只换实现，上层调用不变。
    /// </summary>
    public interface IGameSimulation
    {
        /// <summary>本地是否拥有权威（单机、Host 为 true；纯 Client 为 false）。</summary>
        bool IsAuthority { get; }

        /// <summary>本地玩家实体 Id，未生成时为 -1。</summary>
        int LocalEntityId { get; }

        /// <summary>生成玩家实体，返回 entityId。</summary>
        int SpawnPlayer(Vector3 position, bool isLocal);

        /// <summary>施加输入（权威端写入模拟；Client 本地预测）。</summary>
        void ApplyInput(int entityId, in PlayerInputCmd input);

        /// <summary>逻辑步进（固定或每帧均可，由 Runner 决定）。</summary>
        void Tick(float dt);

        /// <summary>取用于渲染的位姿（已含插值 / 预测结果）。</summary>
        bool TryGetPose(int entityId, out EntityPose pose);

        /// <summary>移除实体。</summary>
        void Despawn(int entityId);
    }
}
