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

        [SyncVarFlags(SyncFlags.Interpolated | SyncFlags.LagCompensated)]
        SyncVar<Vector3> _position;

        /// <summary>角色身体朝向（模型用，会朝移动方向转）。</summary>
        [SyncVarFlags(SyncFlags.Interpolated)]
        SyncVar<float> _yaw;

        Vector3 _velocity;
        bool _grounded = true;
        bool _driveLocally;
        ActPlayerInput _cmd;

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
        public bool DriveLocally => _driveLocally;

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
            _faceHoldLeft = 0f;
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

            // 视线 Yaw = LocalLookInput（用于相对移动 + 本地相机）
            _lookYaw = _cmd.Rotation;

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

            if (!LocalLookInput.CursorLocked)
                return ActPlayerInput.FromAxes(0f, 0f, LocalLookInput.Yaw, false, false);

            return ActPlayerInput.FromAxes(x, y, LocalLookInput.Yaw, sprint, jump);

            /* ---- 旧硬编码（已由 GameInput + LocalLookInput 替代）----
            var kb = Keyboard.current;
            float x = 0f, y = 0f;
            bool sprint = false;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
                sprint = kb.leftShiftKey.isPressed;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                var stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.01f) { x = stick.x; y = stick.y; }
                if (pad.leftShoulder.isPressed || pad.leftStickButton.isPressed) sprint = true;
            }
            if (!LocalLookInput.CursorLocked) { x = 0f; y = 0f; sprint = false; }
            bool jump = LocalLookInput.ConsumeJump();
            return ActPlayerInput.FromAxes(x, y, LocalLookInput.Yaw, sprint, jump);
            ---- */
        }

        /* ---- 旧仅向下 Raycast 贴地（无水平挡墙，侧面可穿进斜坡）----
        static Vector3 Snap(Vector3 pos, ref Vector3 vel, ref bool grounded)
        {
            var origin = pos + Vector3.up * 2f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                grounded = true;
                if (vel.y < 0f) vel.y = -2f;
                return hit.point + Vector3.up * 0.02f;
            }
            grounded = false;
            return pos;
        }
        ---- */

    }
}
