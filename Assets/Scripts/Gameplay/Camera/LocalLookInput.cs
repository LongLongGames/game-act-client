using UnityEngine;
using UnityEngine.InputSystem;
using GameAct.Input;

namespace GameAct.Gameplay.Camera
{
    /// <summary>
    /// 本地玩家的「视角输入」唯一来源：Look Action（鼠标 delta / 右摇杆）→ Yaw、Pitch。
    ///
    /// 为什么要单独抽出来：
    /// - 视角必须按【渲染帧】累加（每帧读一次 Look），相机每帧直接读它，才会丝滑。
    /// - 逻辑层（ActPlayer / ActPlayerController）只在采样输入时【读取当前值】，
    ///   用它把 WASD 转成世界方向。逻辑 tick 是 30Hz，如果在 tick 里自己累加 mouse.delta，
    ///   没跑 tick 的帧的鼠标增量会丢，一帧跑两个 tick 又会重复累加。
    ///
    /// 数据流是单向的：输入 → 这里 → (相机每帧读 / 逻辑采样时读)，相机的平滑结果绝不回写这里。
    /// </summary>
    public static class LocalLookInput
    {
        /// <summary>鼠标每像素转多少度（原来的 2.0 * 0.1）。</summary>
        public static float MouseDegPerPixel = 0.2f;
        public static float StickYawDegPerSec = 300f;
        public static float StickPitchDegPerSec = 150f;

        public static float MinPitch = -10f;
        public static float MaxPitch = 65f;

        /// <summary>视线水平朝向（度）。0 = 朝 +Z，增大朝 +X（和 ActPlayer 的 sin/cos 约定一致）。</summary>
        public static float Yaw { get; private set; }

        /// <summary>视线俯仰（度）。正 = 向下看（相机升高）。</summary>
        public static float Pitch { get; private set; } = 12f;

        public static bool CursorLocked { get; private set; } = true;

        static bool _active;
        static bool _jumpLatched;
        static Driver _driver;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _active = false;
            _jumpLatched = false;
            _driver = null;
        }

        /// <summary>开局调用（GameplayRunner.StartSession）。</summary>
        public static void Begin(float yaw = 0f, float pitch = 12f)
        {
            Yaw = yaw;
            Pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
            _jumpLatched = false;
            _active = true;
            SetCursorLocked(true);
            EnsureDriver();
        }

        /// <summary>退局调用（GameplayRunner.StopSession）。</summary>
        public static void End()
        {
            _active = false;
            _jumpLatched = false;
            SetCursorLocked(false);
        }

        public static void SetCursorLocked(bool locked)
        {
            CursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        /// <summary>
        /// 跳跃按键锁存：wasPressedThisFrame 只在按下那一帧为 true，
        /// 而 Solo/Host 的输入是在 30Hz 的逻辑 tick 里采样的，会漏掉没跑 tick 的帧。
        /// 这里每帧锁存，逻辑采样时消费一次。
        /// </summary>
        public static bool ConsumeJump()
        {
            bool j = _jumpLatched;
            _jumpLatched = false;
            return j;
        }

        static void EnsureDriver()
        {
            if (_driver != null) return;
            var go = new GameObject("[LocalLookInput]");
            Object.DontDestroyOnLoad(go);
            _driver = go.AddComponent<Driver>();
        }

        static void Tick(float dt)
        {
            if (!_active) return;

            // Esc / 手柄 Start 切换光标
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                SetCursorLocked(!CursorLocked);
            var pad = Gamepad.current;
            if (pad != null && pad.startButton.wasPressedThisFrame)
                SetCursorLocked(!CursorLocked);

            // Jump 锁存（Action：Space / 已绑定手柄键）
            if (GameInput.JumpPressed)
                _jumpLatched = true;

            if (!CursorLocked) return;

            // Look：同一 Action 绑 Mouse/delta 与 Gamepad/rightStick。
            // 语义不同（像素 vs -1~1），按 activeControl 设备分支；两路可同时累加（无缝切换）。
            ApplyLookFromAction(dt);
            ApplyLookDeviceFallback(dt);

            Pitch = Mathf.Clamp(Pitch, MinPitch, MaxPitch);
            if (Yaw > 360f) Yaw -= 360f;
            else if (Yaw < -360f) Yaw += 360f;
        }

        static void ApplyLookFromAction(float dt)
        {
            if (!GameInput.IsReady) return;
            var action = GameInput.Player.Look;
            if (!action.enabled) return;

            // 遍历本帧有值的控件，避免只读合成值导致鼠标/摇杆尺度混淆
            var controls = action.controls;
            for (int i = 0; i < controls.Count; i++)
            {
                var c = controls[i];
                if (c == null || !c.IsActuated(0.01f)) continue;

                Vector2 v = Vector2.zero;
                if (c is InputControl<Vector2> vc)
                    v = vc.ReadValue();
                else
                    continue;

                if (c.device is Mouse)
                {
                    Yaw += v.x * MouseDegPerPixel;
                    Pitch -= v.y * MouseDegPerPixel;
                }
                else if (c.device is Gamepad)
                {
                    if (v.sqrMagnitude > 0.01f)
                    {
                        Yaw += v.x * StickYawDegPerSec * dt;
                        Pitch -= v.y * StickPitchDegPerSec * dt;
                    }
                }
            }
        }

        /// <summary>Action 控件列表异常时的兜底（仍键鼠+手柄双通）。</summary>
        static void ApplyLookDeviceFallback(float dt)
        {
            // 若 Action 已成功读到控件则不必再读设备；用静态标记避免双加
            // 简化：仅在 Action 未启用时走设备
            if (GameInput.IsReady && GameInput.Player.Look.enabled)
                return;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 d = mouse.delta.ReadValue();
                Yaw += d.x * MouseDegPerPixel;
                Pitch -= d.y * MouseDegPerPixel;
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 s = pad.rightStick.ReadValue();
                if (s.sqrMagnitude > 0.01f)
                {
                    Yaw += s.x * StickYawDegPerSec * dt;
                    Pitch -= s.y * StickPitchDegPerSec * dt;
                }
            }
        }

        /* ---- 旧硬编码（已由 GameInput.Player.Look / Jump 替代）----
        static void Tick_OLD(float dt)
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                SetCursorLocked(!CursorLocked);
            if (kb != null && kb.spaceKey.wasPressedThisFrame) _jumpLatched = true;
            if (pad != null && pad.buttonSouth.wasPressedThisFrame) _jumpLatched = true;
            if (!CursorLocked) return;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 d = mouse.delta.ReadValue();
                Yaw += d.x * MouseDegPerPixel;
                Pitch -= d.y * MouseDegPerPixel;
            }
            if (pad != null)
            {
                Vector2 look = pad.rightStick.ReadValue();
                if (look.sqrMagnitude > 0.01f)
                {
                    Yaw += look.x * StickYawDegPerSec * dt;
                    Pitch -= look.y * StickPitchDegPerSec * dt;
                }
            }
        }
        ---- */

        /// <summary>最早执行，保证本帧后面所有脚本读到的都是本帧最新的视角。</summary>
        [DefaultExecutionOrder(-1000)]
        sealed class Driver : MonoBehaviour
        {
            void Update() => Tick(Time.unscaledDeltaTime);
        }
    }
}
