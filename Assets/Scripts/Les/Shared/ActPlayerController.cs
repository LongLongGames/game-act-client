using LiteEntitySystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameAct.Les.Shared
{
    /// <summary>
    /// 人类控制器：Client 在 VisualUpdate 写 PendingInput；权威在 BeforeControlledUpdate 应用到 ActPlayer。
    /// </summary>
    public class ActPlayerController : HumanControllerLogic<ActPlayerInput, ActPlayer>
    {
        public ActPlayerController(EntityParams entityParams) : base(entityParams) { }

        protected override void VisualUpdate()
        {
            if (ControlledEntity == null) return;

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
            pending = ActPlayerInput.FromAxes(x, y, sprint, jump);
        }

        protected override void BeforeControlledUpdate()
        {
            ControlledEntity?.SetInput(CurrentInput);
        }
    }
}
