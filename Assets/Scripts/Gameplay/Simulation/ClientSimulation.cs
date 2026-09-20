using System.Collections.Generic;
using UnityEngine;

namespace GameAct.Gameplay.Simulation
{
    /// <summary>
    /// Client 模拟：本地玩家预测 + 远程实体快照插值。
    /// 当前 P1：预测与 LocalSimulation 同逻辑；远程缓冲预留，无快照时不显示。
    /// </summary>
    public sealed class ClientSimulation : IGameSimulation
    {
        public bool IsAuthority => false;
        public int LocalEntityId => _local.LocalEntityId;

        readonly LocalSimulation _local = new LocalSimulation(); // 本地预测复用权威积分
        readonly Dictionary<int, SnapshotSample> _remote = new Dictionary<int, SnapshotSample>();
        readonly Dictionary<int, EntityPose> _remoteRender = new Dictionary<int, EntityPose>();

        const float InterpDelay = 0.1f; // 100ms 插值延迟

        struct SnapshotSample
        {
            public EntityPose From;
            public EntityPose To;
            public float FromTime;
            public float ToTime;
        }

        public int SpawnPlayer(Vector3 position, bool isLocal)
        {
            if (isLocal)
                return _local.SpawnPlayer(position, true);

            // 远程：只登记，等快照
            int id = _local.SpawnPlayer(position, false);
            // 从本地字典移除权威体，改走插值表（简化：远程 id 用负号区分）
            _local.Despawn(id);
            int remoteId = -id;
            _remote[remoteId] = new SnapshotSample
            {
                From = new EntityPose { Position = position, Rotation = Quaternion.identity, Grounded = true },
                To = new EntityPose { Position = position, Rotation = Quaternion.identity, Grounded = true },
                FromTime = Time.time,
                ToTime = Time.time
            };
            return remoteId;
        }

        public void BindCharacterController(int entityId, CharacterController cc)
        {
            if (entityId == LocalEntityId)
                _local.BindCharacterController(entityId, cc);
        }

        public void ApplyInput(int entityId, in PlayerInputCmd input)
        {
            // 仅本地预测
            if (entityId == LocalEntityId)
                _local.ApplyInput(entityId, input);
        }

        public void Tick(float dt)
        {
            _local.Tick(dt);
            SampleRemotes(Time.time - InterpDelay);
        }

        /// <summary>网络层推入远程快照（后续 StateSync 调用）。</summary>
        public void PushRemoteSnapshot(int entityId, in EntityPose pose, float serverTime)
        {
            if (!_remote.TryGetValue(entityId, out var s))
            {
                s = new SnapshotSample { From = pose, To = pose, FromTime = serverTime, ToTime = serverTime };
            }
            else
            {
                s.From = s.To;
                s.FromTime = s.ToTime;
                s.To = pose;
                s.ToTime = serverTime;
            }
            _remote[entityId] = s;
        }

        void SampleRemotes(float renderTime)
        {
            _remoteRender.Clear();
            foreach (var kv in _remote)
            {
                var s = kv.Value;
                float span = Mathf.Max(0.0001f, s.ToTime - s.FromTime);
                float t = Mathf.Clamp01((renderTime - s.FromTime) / span);
                var pose = new EntityPose
                {
                    Position = Vector3.Lerp(s.From.Position, s.To.Position, t),
                    Rotation = Quaternion.Slerp(s.From.Rotation, s.To.Rotation, t),
                    Velocity = Vector3.Lerp(s.From.Velocity, s.To.Velocity, t),
                    Grounded = t > 0.5f ? s.To.Grounded : s.From.Grounded
                };
                _remoteRender[kv.Key] = pose;
            }
        }

        public bool TryGetPose(int entityId, out EntityPose pose)
        {
            if (entityId == LocalEntityId)
                return _local.TryGetPose(entityId, out pose);
            if (_remoteRender.TryGetValue(entityId, out pose))
                return true;
            if (_remote.TryGetValue(entityId, out var s))
            {
                pose = s.To;
                return true;
            }
            pose = default;
            return false;
        }

        public void Despawn(int entityId)
        {
            if (entityId == LocalEntityId)
                _local.Despawn(entityId);
            _remote.Remove(entityId);
            _remoteRender.Remove(entityId);
        }
    }
}
