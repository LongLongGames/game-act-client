using System.Collections.Generic;
using UnityEngine;

namespace GameAct.Gameplay.Simulation
{
    /// <summary>
    /// 本地权威模拟：单机与 Host 共用。
    /// 输入立即生效，无网络延迟。
    /// </summary>
    public sealed class LocalSimulation : IGameSimulation
    {
        public bool IsAuthority => true;
        public int LocalEntityId { get; private set; } = -1;

        const float WalkSpeed = 5.5f;
        const float SprintSpeed = 8.5f;
        const float Gravity = -20f;
        const float JumpSpeed = 7.5f;

        struct Body
        {
            public EntityPose Pose;
            public bool IsLocal;
            public CharacterController Cc; // 可选，由外部绑定；无则纯积分
        }

        readonly Dictionary<int, Body> _bodies = new Dictionary<int, Body>();
        readonly Dictionary<int, PlayerInputCmd> _inputs = new Dictionary<int, PlayerInputCmd>();
        int _nextId = 1;

        public int SpawnPlayer(Vector3 position, bool isLocal)
        {
            int id = _nextId++;
            var pose = EntityPose.Identity;
            pose.Position = position;
            _bodies[id] = new Body { Pose = pose, IsLocal = isLocal };
            if (isLocal) LocalEntityId = id;
            return id;
        }

        /// <summary>可选：把场景里的 CharacterController 绑到实体，便于碰撞。</summary>
        public void BindCharacterController(int entityId, CharacterController cc)
        {
            if (!_bodies.TryGetValue(entityId, out var b)) return;
            b.Cc = cc;
            _bodies[entityId] = b;
        }

        public void ApplyInput(int entityId, in PlayerInputCmd input)
        {
            if (!_bodies.ContainsKey(entityId)) return;
            _inputs[entityId] = input;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            // 复制 key 列表，避免迭代时修改
            var ids = ListPool.Get(_bodies.Keys);
            foreach (var id in ids)
            {
                if (!_bodies.TryGetValue(id, out var body)) continue;
                _inputs.TryGetValue(id, out var input);
                Integrate(ref body, in input, dt);
                _bodies[id] = body;
            }
            ListPool.Release(ids);
        }

        void Integrate(ref Body body, in PlayerInputCmd input, float dt)
        {
            var pose = body.Pose;
            var move = input.Move;
            if (move.sqrMagnitude > 1f) move.Normalize();

            float speed = input.Sprint ? SprintSpeed : WalkSpeed;
            var wish = new Vector3(move.x, 0f, move.y) * speed;

            // 朝向：有输入则转向移动方向
            if (wish.sqrMagnitude > 0.001f)
            {
                var look = Quaternion.LookRotation(wish.normalized, Vector3.up);
                pose.Rotation = Quaternion.Slerp(pose.Rotation, look, 1f - Mathf.Exp(-12f * dt));
            }

            var vel = pose.Velocity;
            vel.x = wish.x;
            vel.z = wish.z;

            if (pose.Grounded && input.Jump)
            {
                vel.y = JumpSpeed;
                pose.Grounded = false;
            }

            vel.y += Gravity * dt;
            var delta = vel * dt;

            if (body.Cc != null && body.Cc.enabled)
            {
                var flags = body.Cc.Move(delta);
                pose.Position = body.Cc.transform.position;
                pose.Grounded = (flags & CollisionFlags.Below) != 0;
                if (pose.Grounded && vel.y < 0f) vel.y = -2f; // 轻微贴地力，别用 0 以免判定抖动
            }
            else
            {
                // CC 未启用：只平移，并用射线贴地，避免自由落体穿模
                pose.Position += new Vector3(delta.x, 0f, delta.z);
                var origin = pose.Position + Vector3.up * 2f;
                if (Physics.Raycast(origin, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore))
                {
                    pose.Position = hit.point + Vector3.up * 0.02f;
                    vel.y = 0f;
                    pose.Grounded = true;
                }
                else
                {
                    pose.Position += new Vector3(0f, delta.y, 0f);
                    pose.Grounded = false;
                }
            }

            pose.Velocity = vel;
            body.Pose = pose;
        }

        public bool TryGetPose(int entityId, out EntityPose pose)
        {
            if (_bodies.TryGetValue(entityId, out var b))
            {
                pose = b.Pose;
                return true;
            }
            pose = default;
            return false;
        }

        public void Despawn(int entityId)
        {
            _bodies.Remove(entityId);
            _inputs.Remove(entityId);
            if (LocalEntityId == entityId) LocalEntityId = -1;
        }

        /// <summary>极简 List 复用，避免 Tick 分配。</summary>
        static class ListPool
        {
            static readonly List<int> Buffer = new List<int>(16);
            public static List<int> Get(Dictionary<int, Body>.KeyCollection keys)
            {
                Buffer.Clear();
                Buffer.AddRange(keys);
                return Buffer;
            }
            public static void Release(List<int> _) { }
        }
    }
}
