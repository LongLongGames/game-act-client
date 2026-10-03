using LiteEntitySystem;
using UnityEngine;
using UnityEngine.InputSystem;
using GameAct.Gameplay.Camera;
using GameAct.Input;
using GameAct.Spatial;

namespace GameAct.Les.Shared
{
    /// <summary>
    /// LES 玩家 Pawn。
    /// 控制手感（动作 / RoR2 向）：
    /// - 视线 Yaw 来自 LocalLookInput（键鼠/手柄无缝）
    /// - WASD / 左摇杆相对视线方向移动（GameInput.Move）
    /// - 角色模型朝向移动方向转身（有转向速度），按 A/S/D 会转过去
    /// - 平A 索敌时 RequestFaceDirection：身体优先转向目标，持续 FaceHold 秒
    /// </summary>
    public class ActPlayer : PawnLogic
    {
        const float WalkSpeed = 5.5f;
        const float SprintSpeed = 8.5f;
        const float Gravity = -20f;
        const float JumpSpeed = 7.5f;
        /// <summary>角色转向移动方向的角速度（度/秒）。</summary>
        const float TurnSpeed = 720f;
        /// <summary>平A 索敌转向角速度（度/秒），略快一点手感更跟手。</summary>
        const float FaceTurnSpeed = 900f;
        /// <summary>出拳索敌后身体强制朝向目标的保持时间（秒），与 PlayerCombatDriver.FaceHoldSeconds 默认值一致。</summary>
        const float AttackFaceHold = 0.4f;

        [SyncVarFlags(SyncFlags.Interpolated | SyncFlags.LagCompensated)]
        SyncVar<Vector3> _position;

        /// <summary>角色身体朝向（模型用，会朝移动方向转）。</summary>
        [SyncVarFlags(SyncFlags.Interpolated)]
        SyncVar<float> _yaw;

        // ─── 远程表现同步 ───────────────────────────────────────────────
        // 非权威端的远程 ActPlayer 不跑 Update，_velocity / _grounded 永远是初值，
        // 远程 PlayerView 因此不播走/跑/跳/出拳。这些值由权威端写、全员读。
        /// <summary>水平速度（动画 Movement 混合用）。</summary>
        SyncVar<float> _animSpeedXZ;
        /// <summary>竖直速度（动画 IsJumping 判定用，贴地时为 0）。</summary>
        SyncVar<float> _animVelY;
        SyncVar<bool> _animGrounded;
        /// <summary>起跳计数（每次起跳 +1，溢出回绕），View 监听变化播一次 Jump。</summary>
        SyncVar<byte> _jumpCount;
        /// <summary>出拳计数（每次平A +1，溢出回绕），View 监听变化播一次 Attack。</summary>
        SyncVar<byte> _attackCount;

        // ─── 预测状态（必须是 SyncVar，才会参与 LES 回滚）────────────────
        // 普通字段不会被回滚：Client 收到快照回滚到服务器状态后重放输入时，
        // 若 _velocity.y / _grounded 还是「预测到未来」的旧值，就会出现：
        //   起跳被重放成二段跳 / 刚跳起来就被 Snap 拽回地面（跳一半贴地）。
        SyncVar<float> _simVelY;
        SyncVar<bool> _simGrounded;

        Vector3 _velocity;
        bool _grounded = true;
        bool _driveLocally;
        ActPlayerInput _cmd;

        // 出拳序号（权威端/预测端各自对比 _cmd.AttackSeq 的变化）
        byte _lastAttackSeq;
        bool _attackSeqInited;

        /// <summary>本 tick 是否刚起跳（表现层可消费一次）。</summary>
        bool _jumpedThisTick;

        /// <summary>最近一个 tick 采样到的视线 Yaw（来自 LocalLookInput，不单独同步）。相机不要用它，相机每帧读 LocalLookInput。</summary>
        float _lookYaw;

        // 平A 索敌：强制身体朝向
        float _faceTargetYaw;
        float _faceHoldLeft;

        public Vector3 Position => _position.Value;
        /// <summary>身体朝向（模型）。</summary>
        public float Yaw => _yaw.Value;
        /// <summary>视线 / 相机朝向（鼠标）。本地移动与相机都用这个。</summary>
        public float LookYaw => _lookYaw;

        /// <summary>
        /// true = 本端是权威（Solo / Host），SyncVar 只有 tick 阶梯值，表现层需要自己做渲染插值；
        /// false = Client，用 InterpolatedValue 即可。
        /// </summary>
        public bool RenderNeedsSmoothing => !EntityManager.IsClient;
        public Vector3 Velocity => _velocity;

        /// <summary>全端可读：水平速度（远程 PlayerView 驱动 Movement）。</summary>
        public float AnimSpeedXZ => _animSpeedXZ.Value;
        /// <summary>全端可读：竖直速度。</summary>
        public float AnimVelY => _animVelY.Value;
        /// <summary>全端可读：是否贴地。</summary>
        public bool AnimGrounded => _animGrounded.Value;
        /// <summary>全端可读：起跳计数。</summary>
        public byte JumpCount => _jumpCount.Value;
        /// <summary>全端可读：出拳计数。</summary>
        public byte AttackCount => _attackCount.Value;

        public bool DriveLocally => _driveLocally;
        /// <summary>是否贴地（供表现层驱动 IsGrounded）。</summary>
        public bool Grounded => _grounded;

        /// <summary>
        /// 消费本 tick 的起跳标记。返回 true 表示刚起跳，表现层应播一次 Jump。
        /// </summary>
        public bool ConsumeJumpedThisTick()
        {
            if (!_jumpedThisTick) return false;
            _jumpedThisTick = false;
            return true;
        }

        public Vector3 InterpolatedPosition =>
            EntityManager.IsClient ? _position.InterpolatedValue : _position.Value;

        public float InterpolatedYaw =>
            EntityManager.IsClient ? _yaw.InterpolatedValue : _yaw.Value;

        public ActPlayer(EntityParams entityParams) : base(entityParams) { }

        public void Spawn(Vector3 position)
        {
            _position.Value = position;
            _yaw.Value = 0f;
            _lookYaw = 0f;
            _velocity = Vector3.zero;
            _grounded = true;
            _simVelY.Value = 0f;
            _simGrounded.Value = true;
            _faceHoldLeft = 0f;
            _jumpedThisTick = false;
            _animSpeedXZ.Value = 0f;
            _animVelY.Value = 0f;
            _animGrounded.Value = true;
            _attackSeqInited = false;
        }

        public void SetDriveLocally(bool on) => _driveLocally = on;

        public void SetInput(in ActPlayerInput cmd) => _cmd = cmd;

        /// <summary>
        /// 平A / 技能索敌：身体转向 worldDir（XZ），在 holdSeconds 内优先于移动转向。
        /// 由 PlayerCombatDriver 在命中索敌后调用。
        /// </summary>
        public void RequestFaceDirection(Vector3 worldDir, float holdSeconds = 0.35f)
        {
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 1e-6f) return;
            _faceTargetYaw = Mathf.Atan2(worldDir.x, worldDir.z) * Mathf.Rad2Deg;
            _faceHoldLeft = Mathf.Max(0.05f, holdSeconds);
        }

        protected override void Update()
        {
            base.Update();

            if (_driveLocally && Controller == null)
                _cmd = ReadLocalInput();

            Integrate(EntityManager.DeltaTimeF);
        }

        void Integrate(float dt)
        {
            if (dt <= 0f) return;

            // 从（可能刚被回滚过的）SyncVar 载入纵向状态
            _velocity.y = _simVelY.Value;
            _grounded = _simGrounded.Value;

            // 视线 Yaw = LocalLookInput（用于相对移动 + 本地相机）
            _lookYaw = _cmd.Rotation;

            // 出拳：序号变化 = 新的一拳。首帧只采纳序号（防止残留序号误触发）。
            if (!_attackSeqInited)
            {
                _lastAttackSeq = _cmd.AttackSeq;
                _attackSeqInited = true;
            }
            else if (unchecked((sbyte)(_cmd.AttackSeq - _lastAttackSeq)) > 0)
            {
                // 只认「向前」的序号：回滚重放的历史输入序号 <= 已处理序号，直接忽略
                _lastAttackSeq = _cmd.AttackSeq;
                if (_cmd.AttackFace)
                {
                    float r = _cmd.AttackYaw * Mathf.Deg2Rad;
                    RequestFaceDirection(new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r)), AttackFaceHold);
                }
                // 只有权威端写计数，Client 的预测副本不写（由快照覆盖）
                if (!EntityManager.IsClient)
                    unchecked { _attackCount.Value = (byte)(_attackCount.Value + 1); }
            }

            // 本地输入 → 相对视线的世界速度
            var move = new Vector2(_cmd.MoveX, _cmd.MoveY);
            if (move.sqrMagnitude > 1f) move.Normalize();

            float speed = _cmd.Sprint ? SprintSpeed : WalkSpeed;
            float lookRad = _lookYaw * Mathf.Deg2Rad;
            Vector3 forward = new Vector3(Mathf.Sin(lookRad), 0f, Mathf.Cos(lookRad));
            Vector3 right = new Vector3(Mathf.Cos(lookRad), 0f, -Mathf.Sin(lookRad));
            Vector3 wish = (forward * move.y + right * move.x) * speed;

            _velocity.x = wish.x;
            _velocity.z = wish.z;

            // 身体朝向：平A 索敌优先，其次有移动时朝移动方向
            if (_faceHoldLeft > 0f)
            {
                _faceHoldLeft -= dt;
                _yaw.Value = Mathf.MoveTowardsAngle(_yaw.Value, _faceTargetYaw, FaceTurnSpeed * dt);
            }
            else
            {
                float wishHorizSq = wish.x * wish.x + wish.z * wish.z;
                if (wishHorizSq > 0.001f)
                {
                    float targetYaw = Mathf.Atan2(wish.x, wish.z) * Mathf.Rad2Deg;
                    _yaw.Value = Mathf.MoveTowardsAngle(_yaw.Value, targetYaw, TurnSpeed * dt);
                }
            }

            if (_grounded && _cmd.Jump)
            {
                _velocity.y = JumpSpeed;
                _grounded = false;
                _jumpedThisTick = true;
                if (!EntityManager.IsClient)
                    unchecked { _jumpCount.Value = (byte)(_jumpCount.Value + 1); }
            }

            _velocity.y += Gravity * dt;

            // 水平只挡墙；Y 只在空中积分，贴地交给 Snap（避免每帧插入地面再弹回导致抖）
            Vector3 horiz = new Vector3(_velocity.x, 0f, _velocity.z) * dt;
            Vector3 pos = _position.Value;
            float velY = _velocity.y;
            if (!_grounded)
                pos.y += velY * dt;
            pos = WorldMotor.SlideMove(pos, horiz, WorldMotor.DefaultRadius, WorldMotor.DefaultHeight, WorldMotor.EnvironmentMask);
            pos = WorldMotor.SnapToGround(pos, ref velY, ref _grounded, WorldMotor.EnvironmentMask);
            _velocity.y = velY;
            _position.Value = pos;
            _simVelY.Value = velY;
            _simGrounded.Value = _grounded;

            // 同步给远程表现（权威端写；量化 + 贴地归零，避免每 tick 无意义脏数据）
            if (!EntityManager.IsClient)
            {
                float spd = Mathf.Sqrt(_velocity.x * _velocity.x + _velocity.z * _velocity.z);
                _animSpeedXZ.Value = Mathf.Round(spd * 10f) / 10f;
                _animGrounded.Value = _grounded;
                _animVelY.Value = _grounded ? 0f : Mathf.Round(_velocity.y * 10f) / 10f;
            }
        }

        /// <summary>
        /// Solo / Host 本地输入采样（在逻辑 tick 里调用）。
        /// 视角 yaw 不在这里累加——由 LocalLookInput 按渲染帧累加，这里只读当前值。
        /// </summary>
        static ActPlayerInput ReadLocalInput()
        {
            Vector2 move = GameInput.Move;
            float x = move.x, y = move.y;
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
                x = move.x;
                y = move.y;
            }

            bool sprint = GameInput.SprintHeld;
            bool jump = LocalLookInput.ConsumeJump() || GameInput.JumpPressed;

            ActPlayerInput cmd;
            if (!LocalLookInput.CursorLocked)
                cmd = ActPlayerInput.FromAxes(0f, 0f, LocalLookInput.Yaw, false, false);
            else
                cmd = ActPlayerInput.FromAxes(x, y, LocalLookInput.Yaw, sprint, jump);

            LocalActionInput.Stamp(ref cmd);
            return cmd;
        }
    }
}
