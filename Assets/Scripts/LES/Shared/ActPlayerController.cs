using LiteEntitySystem;
using UnityEngine;
using UnityEngine.InputSystem;
using GameAct.Gameplay.Camera;

namespace GameAct.Les.Shared
{
    /// <summary>
    /// 人类控制器：Client 在 VisualUpdate 写 PendingInput；权威在 BeforeControlledUpdate 应用到 ActPlayer。
    /// 手感对齐 UnityExample：
    /// - 鼠标控制 Yaw（由 LocalLookInput 每渲染帧累加，这里只读当前值）
    /// - WASD 相对当前 Yaw 移动
    /// - Esc 切换光标锁定（由 LocalLookInput 处理）
    /// </summary>
    public class ActPlayerController : HumanControllerLogic<ActPlayerInput, ActPlayer>
    {
        public ActPlayerController(EntityParams entityParams) : base(entityParams)
        {
            // 光标锁定交给 LocalLookInput.Begin。
            // 注意：Host 上每个远端玩家也会 new 一个 Controller，构造函数里不能再碰 Cursor。
        }

        protected override void VisualUpdate()
        {
            if (ControlledEntity == null) return;

            float yaw = LocalLookInput.Yaw;

            if (!LocalLookInput.CursorLocked)
            {
                // 光标解锁时仍写当前 yaw，避免输入断流
                ref var idle = ref ModifyPendingInput();
                idle = ActPlayerInput.FromAxes(0f, 0f, yaw, false, false);
                return;
            }

            // 键盘 / 手柄移动（本地坐标）
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

            ref var pending = ref ModifyPendingInput();
            pending = ActPlayerInput.FromAxes(x, y, yaw, sprint, jump);
        }

        protected override void BeforeControlledUpdate()
        {
            ControlledEntity?.SetInput(CurrentInput);
        }
    }
}
