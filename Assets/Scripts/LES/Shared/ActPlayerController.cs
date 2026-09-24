using LiteEntitySystem;
using UnityEngine;
using UnityEngine.InputSystem;
using GameAct.Input;
using GameAct.Gameplay.Camera;

namespace GameAct.Les.Shared
{
    /// <summary>
    /// 人类控制器：Client 在 VisualUpdate 写 PendingInput；权威在 BeforeControlledUpdate 应用到 ActPlayer。
    /// 手感：
    /// - 视线 Yaw 来自 LocalLookInput（每渲染帧累加，键鼠/手柄无缝）
    /// - Move / Sprint / Jump 来自 GameInputActions（同一 Action 多设备绑定）
    /// - Esc / Start 切换光标由 LocalLookInput 处理
    /// </summary>
    public class ActPlayerController : HumanControllerLogic<ActPlayerInput, ActPlayer>
    {
        public ActPlayerController(EntityParams entityParams) : base(entityParams)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        protected override void VisualUpdate()
        {
            if (ControlledEntity == null) return;

            if (!LocalLookInput.CursorLocked)
            {
                ref var idle = ref ModifyPendingInput();
                idle = ActPlayerInput.FromAxes(0f, 0f, LocalLookInput.Yaw, false, false);
                return;
            }

            Vector2 move = GameInput.Move;
            float x = move.x;
            float y = move.y;
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
                x = move.x;
                y = move.y;
            }

            bool sprint = GameInput.SprintHeld;
            // Jump：优先消费 LocalLookInput 锁存（抗 30Hz 漏帧），否则读 Action
            bool jump = LocalLookInput.ConsumeJump() || GameInput.JumpPressed;

            ref var pending = ref ModifyPendingInput();
            pending = ActPlayerInput.FromAxes(x, y, LocalLookInput.Yaw, sprint, jump);

            /* ---- 旧硬编码（已由 GameInput + LocalLookInput 替代）----
            float _yaw = 0f;
            float _mouseSensitivity = 2.0f;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { ... }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float mouseX = mouse.delta.x.ReadValue() * _mouseSensitivity * 0.1f;
                _yaw += mouseX;
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
                    _yaw += look.x * _mouseSensitivity * 2.5f;
            }
            pending = ActPlayerInput.FromAxes(x, y, _yaw, sprint, jump);
            ---- */
        }

        protected override void BeforeControlledUpdate()
        {
            ControlledEntity?.SetInput(CurrentInput);
        }
    }
}
