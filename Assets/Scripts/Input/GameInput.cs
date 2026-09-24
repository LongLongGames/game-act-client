using UnityEngine;
using UnityEngine.InputSystem;

namespace GameAct.Input
{
    /// <summary>
    /// 全局输入入口：持有一份 GameInputActions，键鼠 / 手柄共用同一套 Action。
    /// Input System 按绑定自动选当前活跃设备，无需手动切换。
    /// </summary>
    public static class GameInput
    {
        static GameInputActions _actions;
        static bool _playerEnabled;

        public static GameInputActions Actions
        {
            get
            {
                Ensure();
                return _actions;
            }
        }

        public static GameInputActions.PlayerActions Player => Actions.Player;
        public static GameInputActions.MenuActions Menu => Actions.Menu;

        public static bool IsReady => _actions != null;

        static void Ensure()
        {
            if (_actions != null) return;
            _actions = new GameInputActions();
        }

        /// <summary>开局启用 Player map（菜单可单独 Enable Menu）。</summary>
        public static void EnablePlayer()
        {
            Ensure();
            if (_playerEnabled) return;
            _actions.Player.Enable();
            _playerEnabled = true;
        }

        public static void DisablePlayer()
        {
            if (_actions == null || !_playerEnabled) return;
            _actions.Player.Disable();
            _playerEnabled = false;
        }

        public static void EnableMenu()
        {
            Ensure();
            _actions.Menu.Enable();
        }

        public static void DisableMenu()
        {
            if (_actions == null) return;
            _actions.Menu.Disable();
        }

        /// <summary>退局 / 销毁时释放。</summary>
        public static void Shutdown()
        {
            if (_actions == null) return;
            _actions.Disable();
            _actions.Dispose();
            _actions = null;
            _playerEnabled = false;
        }

        // ---- 便捷读取（逻辑 tick / 渲染帧均可） ----

        public static Vector2 Move =>
            _actions != null && _playerEnabled
                ? _actions.Player.Move.ReadValue<Vector2>()
                : Vector2.zero;

        public static Vector2 Look =>
            _actions != null && _playerEnabled
                ? _actions.Player.Look.ReadValue<Vector2>()
                : Vector2.zero;

        public static bool JumpPressed =>
            _actions != null && _playerEnabled && _actions.Player.Jump.WasPressedThisFrame();

        public static bool JumpHeld =>
            _actions != null && _playerEnabled && _actions.Player.Jump.IsPressed();

        /// <summary>asset 里 action 名是 Sprine（拼写如此）。</summary>
        public static bool SprintHeld
        {
            get
            {
                if (_actions != null && _playerEnabled && _actions.Player.Sprine.IsPressed())
                    return true;
                // inputed 暂无手柄 Sprint 绑定：兜底 LB / L3
                var pad = Gamepad.current;
                if (pad != null && (pad.leftShoulder.isPressed || pad.leftStickButton.isPressed))
                    return true;
                return false;
            }
        }

        public static bool AttackPressed =>
            _actions != null && _playerEnabled && _actions.Player.Attack.WasPressedThisFrame();

        public static bool Skill1Pressed
        {
            get
            {
                if (_actions != null && _playerEnabled && _actions.Player.Skill1.WasPressedThisFrame())
                    return true;
                // inputed 暂无手柄 Skill 绑定：RB / X
                var pad = Gamepad.current;
                if (pad != null && (pad.rightShoulder.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame))
                    return true;
                return false;
            }
        }

        public static bool Skill2Pressed
        {
            get
            {
                if (_actions != null && _playerEnabled && _actions.Player.Skill2.WasPressedThisFrame())
                    return true;
                // RT / B（避开 Jump=North、Attack=South）
                var pad = Gamepad.current;
                if (pad != null && (pad.rightTrigger.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame))
                    return true;
                return false;
            }
        }

        public static bool InteractPressed =>
            _actions != null && _playerEnabled && _actions.Player.Interact.WasPressedThisFrame();

        public static bool OpenMapPressed =>
            _actions != null && _playerEnabled && _actions.Player.OpenMap.WasPressedThisFrame();
    }
}
