using LiteEntitySystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameAct.Les.Shared
{
    /// <summary>
    /// LES 玩家 Pawn。Solo/Host 本地可用 DriveLocally 直接读输入；
    /// 远端 / Client 由 ActPlayerController 驱动。
    /// </summary>
    public class ActPlayer : PawnLogic
    {
        const float WalkSpeed = 5.5f;
        const float SprintSpeed = 8.5f;
        const float Gravity = -20f;
        const float JumpSpeed = 7.5f;

        [SyncVarFlags(SyncFlags.Interpolated | SyncFlags.LagCompensated)]
        SyncVar<Vector3> _position;

        [SyncVarFlags(SyncFlags.Interpolated)]
        SyncVar<float> _yaw;

        Vector3 _velocity;
        bool _grounded = true;
        bool _driveLocally;
        ActPlayerInput _cmd;

        public Vector3 Position => _position.Value;
        public float Yaw => _yaw.Value;
        public Vector3 Velocity => _velocity;
        public bool DriveLocally => _driveLocally;

        public ActPlayer(EntityParams entityParams) : base(entityParams) { }

        public void Spawn(Vector3 position)
        {
            _position.Value = position;
            _yaw.Value = 0f;
            _velocity = Vector3.zero;
            _grounded = true;
        }

        public void SetDriveLocally(bool on) => _driveLocally = on;

        public void SetInput(in ActPlayerInput cmd) => _cmd = cmd;

        // 跨程序集：protected override
        protected override void Update()
        {
            base.Update(); // Controller.BeforeControlledUpdate → SetInput

            if (_driveLocally && Controller == null)
                _cmd = ReadLocalInput();

            Integrate(EntityManager.DeltaTimeF);
        }

        void Integrate(float dt)
        {
            if (dt <= 0f) return;
            var move = new Vector2(_cmd.MoveX, _cmd.MoveY);
            if (move.sqrMagnitude > 1f) move.Normalize();

            float speed = _cmd.Sprint ? SprintSpeed : WalkSpeed;
            var wish = new Vector3(move.x, 0f, move.y) * speed;

            if (wish.sqrMagnitude > 0.001f)
                _yaw.Value = Mathf.Atan2(wish.x, wish.z) * Mathf.Rad2Deg;

            _velocity.x = wish.x;
            _velocity.z = wish.z;

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

        static ActPlayerInput ReadLocalInput()
        {
            float x = 0f, y = 0f;
            bool sprint = false, jump = false;
            var kb = Keyboard.current;
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
            }
            return ActPlayerInput.FromAxes(x, y, sprint, jump);
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
