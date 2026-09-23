using LiteEntitySystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameAct.Les.Shared
{
    /// <summary>
    /// 人类控制器：Client 在 VisualUpdate 写 PendingInput；权威在 BeforeControlledUpdate 应用到 ActPlayer。
    /// 手感对齐 UnityExample：
    /// - 鼠标控制 Yaw
    /// - WASD 相对当前 Yaw 移动
    /// - Esc 切换光标锁定
    /// </summary>
    public class ActPlayerController : HumanControllerLogic<ActPlayerInput, ActPlayer>
    {
        float _yaw;
        float _mouseSensitivity = 2.0f;
        bool _cursorLocked = true;
        bool _yawInited;

        public ActPlayerController(EntityParams entityParams) : base(entityParams)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        protected override void VisualUpdate()
        {
            if (ControlledEntity == null) return;

            // Esc 切换光标
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                _cursorLocked = !_cursorLocked;
                Cursor.lockState = _cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !_cursorLocked;
            }

            if (!_cursorLocked)
            {
                // 光标解锁时仍写当前 yaw，避免输入断流
                ref var idle = ref ModifyPendingInput();
                idle = ActPlayerInput.FromAxes(0f, 0f, _yaw, false, false);
                return;
            }

            // 初始化 yaw（首次接管时对齐实体）
            if (!_yawInited)
            {
                _yaw = ControlledEntity.Yaw;
                _yawInited = true;
            }

            // 鼠标 Look（与 UnityExample 一致）
            // Input System delta 为像素，*0.1 体感接近旧 Input.GetAxis * 2.0
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float mouseX = mouse.delta.x.ReadValue() * _mouseSensitivity * 0.1f;
                _yaw += mouseX;
            }

            // 键盘 / 手柄移动（本地坐标）
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

                // 右摇杆也可转视角（可选）
                var look = pad.rightStick.ReadValue();
                if (look.sqrMagnitude > 0.01f)
                    _yaw += look.x * _mouseSensitivity * 2.5f;
            }

            ref var pending = ref ModifyPendingInput();
            pending = ActPlayerInput.FromAxes(x, y, _yaw, sprint, jump);
        }

        protected override void BeforeControlledUpdate()
        {
            ControlledEntity?.SetInput(CurrentInput);
        }
    }
}
