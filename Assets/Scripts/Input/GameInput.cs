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

        // ── 走/跑切换（原神式：Ctrl 切换，默认跑步）────────────────
        // Shift 已留给 Dash，不再按住冲刺。
        static bool _runMode = true; // true=跑，false=走
        static bool _ctrlWasDown;

        /// <summary>
        /// 是否处于跑步模式（移动时用 SprintSpeed）。
        /// Ctrl 切换走/跑；默认跑步。Shift 不参与（留给 Dash）。
        /// </summary>
        public static bool SprintHeld
        {
            get
            {
                // 每帧读一次切换（Update/逻辑 tick 都会调到这里）
                PollWalkRunToggle();
                return _runMode;
            }
        }

        /// <summary>当前是否跑步模式（只读，不触发轮询副作用以外的逻辑）。</summary>
        public static bool IsRunMode => _runMode;

        /// <summary>强制设置走/跑（UI 或外部系统可用）。</summary>
        public static void SetRunMode(bool run) => _runMode = run;

        static void PollWalkRunToggle()
        {
            if (!_playerEnabled) return;

            bool ctrlDown = false;
            var kb = Keyboard.current;
            if (kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed))
                ctrlDown = true;

            // 手柄：L3 点击切换走/跑（LB 已给 Dash）
            var pad = Gamepad.current;
            bool stickClick = pad != null && pad.leftStickButton.wasPressedThisFrame;

            if ((ctrlDown && !_ctrlWasDown) || stickClick)
                _runMode = !_runMode;

            _ctrlWasDown = ctrlDown;
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
