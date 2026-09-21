using LiteEntitySystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameAct.Les.Shared
{
    /// <summary>
    /// LES 玩家 Pawn。
    /// 控制手感（动作 / RoR2 向）：
    /// - 鼠标控制「视线 / 相机 Yaw」（Input.Rotation）
    /// - WASD 相对视线方向移动
    /// - 角色模型朝向移动方向转身（有转向速度），按 A/S/D 会转过去
    /// </summary>
    public class ActPlayer : PawnLogic
    {
        const float WalkSpeed = 5.5f;
        const float SprintSpeed = 8.5f;
        const float Gravity = -20f;
        const float JumpSpeed = 7.5f;
        /// <summary>角色转向移动方向的角速度（度/秒）。</summary>
        const float TurnSpeed = 720f;

        [SyncVarFlags(SyncFlags.Interpolated | SyncFlags.LagCompensated)]
        SyncVar<Vector3> _position;

        /// <summary>角色身体朝向（模型用，会朝移动方向转）。</summary>
        [SyncVarFlags(SyncFlags.Interpolated)]
        SyncVar<float> _yaw;

        Vector3 _velocity;
        bool _grounded = true;
        bool _driveLocally;
        ActPlayerInput _cmd;

        /// <summary>上一帧的视线 Yaw（来自鼠标，本地相机用，不单独同步）。</summary>
        float _lookYaw;

        public Vector3 Position => _position.Value;
        /// <summary>身体朝向（模型）。</summary>
        public float Yaw => _yaw.Value;
        /// <summary>视线 / 相机朝向（鼠标）。本地移动与相机都用这个。</summary>
        public float LookYaw => _lookYaw;
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
        }

        public void SetDriveLocally(bool on) => _driveLocally = on;

        public void SetInput(in ActPlayerInput cmd) => _cmd = cmd;

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

            // 视线 Yaw = 鼠标（用于相对移动 + 本地相机）
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

            // 有移动时：身体转向移动方向（A/S/D 会转身）
            // 无移动时：保持当前身体朝向
            float wishHorizSq = wish.x * wish.x + wish.z * wish.z;
            if (wishHorizSq > 0.001f)
            {
                float targetYaw = Mathf.Atan2(wish.x, wish.z) * Mathf.Rad2Deg;
                _yaw.Value = Mathf.MoveTowardsAngle(_yaw.Value, targetYaw, TurnSpeed * dt);
            }

            if (_grounded && _cmd.Jump)
            {
                _velocity.y = JumpSpeed;
                _grounded = false;
            }

            _velocity.y += Gravity * dt;
            var next = _position.Value + _velocity * dt;
            next = Snap(next, ref _velocity, ref _grounded);
            _position.Value = next;
        }

        static float s_localYaw;
        static bool s_localYawInited;
        static bool s_cursorLocked = true;

        static ActPlayerInput ReadLocalInput()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                s_cursorLocked = !s_cursorLocked;
                Cursor.lockState = s_cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !s_cursorLocked;
            }

            if (!s_localYawInited)
            {
                s_localYaw = 0f;
                s_localYawInited = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (s_cursorLocked)
            {
                var mouse = Mouse.current;
                if (mouse != null)
                    s_localYaw += mouse.delta.x.ReadValue() * 2.0f * 0.1f;
            }

            float x = 0f, y = 0f;
            bool sprint = false, jump = false;

            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
                sprint = kb.leftShiftKey.isPressed;
                jump = kb.spaceKey.wasPressedThisFrame;
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                var stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.01f) { x = stick.x; y = stick.y; }
                if (pad.leftShoulder.isPressed || pad.leftStickButton.isPressed) sprint = true;
                if (pad.buttonSouth.wasPressedThisFrame) jump = true;
                var look = pad.rightStick.ReadValue();
                if (look.sqrMagnitude > 0.01f)
                    s_localYaw += look.x * 2.0f * 2.5f;
            }

            return ActPlayerInput.FromAxes(x, y, s_localYaw, sprint, jump);
        }

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
    }
}
