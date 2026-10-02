using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using GameAct.AppFlow;
using GameAct.Input;
using GameAct.UI;

namespace GameAct.Gameplay
{
    /// <summary>
    /// Map1 局内 ESC 暂停菜单（运行时构建 uGUI，无需改场景/Prefab）。
    /// 安全退出走 AppFlow 的托管清理路径，避免 Editor 直接 Stop 触发的 Steam Native 崩溃。
    /// </summary>
    public class GameplayPauseMenu : MonoBehaviour, IGameplayPauseMenu
    {
        public bool IsOpen { get; private set; }

        IAppFlow _flow;
        GameObject _root;
        GameObject _panel;
        bool _busy;

        /// <summary>
        /// 在进局成功后调用。会挂到 DontDestroyOnLoad 的 GameplayRunner 同物体或新建。
        /// </summary>
        public static GameplayPauseMenu Ensure(IAppFlow flow)
        {
            var existing = FindFirstObjectByType<GameplayPauseMenu>();
            if (existing != null)
            {
                existing._flow = flow;
                existing.Close();
                return existing;
            }

            var runner = FindFirstObjectByType<GameplayRunner>();
            GameObject host;
            if (runner != null)
                host = runner.gameObject;
            else
            {
                host = new GameObject("GameplayPauseMenu");
                DontDestroyOnLoad(host);
            }

            var menu = host.GetComponent<GameplayPauseMenu>() ?? host.AddComponent<GameplayPauseMenu>();
            menu._flow = flow;
            menu.BuildUiIfNeeded();
            menu.Close();
            return menu;
        }

        void Update()
        {
            if (_flow == null || _flow.State != AppState.Gameplay)
            {
                if (IsOpen) Close();
                return;
            }

            if (_busy) return;

            // Menu.Pause = ESC / Gamepad Start（已在 GameInputActions 中绑定）
            bool pressed = false;
            try
            {
                if (GameInput.IsReady && GameInput.Actions.Menu.Pause.WasPressedThisFrame())
                    pressed = true;
                else if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                    pressed = true;
            }
            catch
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                    pressed = true;
            }

            if (pressed)
                Toggle();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (IsOpen || _busy) return;
            BuildUiIfNeeded();

            IsOpen = true;
            if (_root != null) _root.SetActive(true);

            // 暂停本地操作输入；Menu map 保持可点 UI
            try
            {
                GameInput.DisablePlayer();
                GameInput.EnableMenu();
            }
            catch { /* ignore */ }

            // 必须走 LocalLookInput：ActPlayer 在 !CursorLocked 时会把 WASD 清零
            // （LocalLookInput 自己也会在 ESC 时 Toggle 锁，这里统一解锁给菜单用鼠标）
            try
            {
                GameAct.Gameplay.Camera.LocalLookInput.SetCursorLocked(false);
            }
            catch
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // 局内不暂停 timeScale：联机/LES 逻辑 tick 依赖真实时间；只挡输入即可
            // Time.timeScale = 0f;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (_root != null) _root.SetActive(false);

            Time.timeScale = 1f;

            try
            {
                // 先恢复 Player map，再锁回光标（Look Action 需要 Player 启用）
                if (_flow != null && _flow.State == AppState.Gameplay)
                {
                    // 防止 _playerEnabled 标志不同步：先 Disable 再 Enable 强制拉起 Player map
                    GameInput.DisablePlayer();
                    GameInput.EnablePlayer();
                }
                // Menu 可继续留着，方便下次 ESC 立刻收到 Pause；也可 Disable
                // GameInput.DisableMenu();
            }
            catch { /* ignore */ }

            // 关键：必须 SetCursorLocked(true)，否则 ActPlayer.ReadLocalInput 一直返回零移动
            try
            {
                if (_flow != null && _flow.State == AppState.Gameplay)
                    GameAct.Gameplay.Camera.LocalLookInput.SetCursorLocked(true);
            }
            catch
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        public void Dispose()
        {
            Close();
            if (_root != null)
            {
                Destroy(_root);
                _root = null;
                _panel = null;
            }
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            if (_root != null)
            {
                Destroy(_root);
                _root = null;
            }
        }

        // ─── 按钮回调 ───────────────────────────────────────

        void OnResume()
        {
            Close();
        }

        void OnLeaveToRoomOrLobby()
        {
            if (_busy) return;
            _busy = true;
            LeaveSafeAsync(quitApp: false).Forget();
        }

        void OnQuitGame()
        {
            if (_busy) return;
            _busy = true;
            LeaveSafeAsync(quitApp: true).Forget();
        }

        async UniTaskVoid LeaveSafeAsync(bool quitApp)
        {
            try
            {
                Close();
                Time.timeScale = 1f;

                if (_flow is AppFlowController controller)
                {
                    if (quitApp)
                        await controller.RequestQuitFromGameplayAsync();
                    else
                        await controller.RequestLeaveGameplayAsync();
                }
                else
                {
                    Debug.LogWarning("[PauseMenu] IAppFlow 不是 AppFlowController，无法走安全退出");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[PauseMenu] LeaveSafe failed: " + e);
            }
            finally
            {
                _busy = false;
            }
        }

        // ─── 运行时 UI ───────────────────────────────────────

        void BuildUiIfNeeded()
        {
            if (_root != null) return;

            EnsureEventSystem();

            _root = new GameObject("PauseMenuCanvas");
            DontDestroyOnLoad(_root);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            _root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _root.AddComponent<GraphicRaycaster>();

            // 半透明遮罩
            var dim = CreateUiObject("Dim", _root.transform);
            var dimRt = dim.GetComponent<RectTransform>();
            StretchFull(dimRt);
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.65f);

            // 中央面板
            _panel = CreateUiObject("Panel", _root.transform);
            var panelRt = _panel.GetComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(360f, 320f);
            panelRt.anchoredPosition = Vector2.zero;
            var panelImg = _panel.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.12f, 0.14f, 0.95f);

            // 标题
            var title = CreateText(_panel.transform, "暂停", 28, FontStyle.Bold);
            var titleRt = title.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(0f, 48f);
            titleRt.anchoredPosition = new Vector2(0f, -24f);

            float y = -90f;
            CreateButton(_panel.transform, "继续游戏", new Vector2(0f, y), OnResume);
            y -= 64f;
            CreateButton(_panel.transform, "返回房间 / 大厅", new Vector2(0f, y), OnLeaveToRoomOrLobby);
            y -= 64f;
            CreateButton(_panel.transform, "退出游戏", new Vector2(0f, y), OnQuitGame);

            _root.SetActive(false);
        }

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            DontDestroyOnLoad(es);
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        static GameObject CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static Text CreateText(Transform parent, string content, int size, FontStyle style)
        {
            var go = CreateUiObject("Text_" + content, parent);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            // Unity 6 内置字体名可能变化，逐个尝试
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                     ?? Resources.GetBuiltinResource<Font>("Arial.ttf")
                     ?? Font.CreateDynamicFontFromOSFont("Arial", size);
            var rt = go.GetComponent<RectTransform>();
            StretchFull(rt);
            return text;
        }

        static void CreateButton(Transform parent, string label, Vector2 anchoredPos, Action onClick)
        {
            var go = CreateUiObject("Btn_" + label, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(280f, 48f);
            rt.anchoredPosition = anchoredPos;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.22f, 0.45f, 0.75f, 1f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.30f, 0.55f, 0.90f, 1f);
            colors.pressedColor = new Color(0.15f, 0.35f, 0.60f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var text = CreateText(go.transform, label, 20, FontStyle.Normal);
            var trt = text.GetComponent<RectTransform>();
            StretchFull(trt);
        }
    }
}
