using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameAct.DebugTools
{
    /// <summary>
    /// 运行时调试台。开关：F12 / ~。仅 Editor / Development Build。
    /// 输入使用 Input System（与工程 Active Input Handling 一致）。
    /// </summary>
    public sealed class DebugConsole
    {
        bool _open;
        string _input = "";
        string _lastResult = "";
        Vector2 _scroll;
        readonly List<string> _log = new List<string>(64);
        const int MaxLog = 80;
        GUIStyle _btnStyle;
        GUIStyle _titleStyle;
        GUIStyle _labelStyle;
        Rect _win = new Rect(20, 40, 720, 720);

        public void Tick()
        {
            if (!IsDebugEnv()) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            // F12 或 `（Backquote）
            if (kb.f12Key.wasPressedThisFrame || kb.backquoteKey.wasPressedThisFrame)
                _open = !_open;
        }

        public void OnGUI()
        {
            if (!_open || !IsDebugEnv()) return;
            EnsureStyles();
            _win = GUI.Window(0xAC706, _win, DrawWindow, "game-act DebugConsole  (F12/~)");
        }

        static bool IsDebugEnv() => Application.isEditor || Debug.isDebugBuild;

        void EnsureStyles()
        {
            if (_btnStyle != null) return;
            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 36,
                padding = new RectOffset(10, 10, 4, 4)
            };
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        }

        void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("命令：输入后回车 / Run；或点列表。刷怪请用 Combat/spawn_monster", _labelStyle);

            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("dbg_input");
            var fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 16, fixedHeight = 32 };
            _input = GUILayout.TextField(_input, fieldStyle, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Run", _btnStyle, GUILayout.Width(80)))
                RunLine(_input);
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return
                && GUI.GetNameOfFocusedControl() == "dbg_input")
            {
                RunLine(_input);
                Event.current.Use();
            }
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_lastResult))
                GUILayout.Label("-> " + _lastResult, _labelStyle);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(320));
            string cat = null;
            foreach (var e in DebugCmd.All)
            {
                if (e.Category != cat)
                {
                    cat = e.Category;
                    GUILayout.Space(8);
                    GUILayout.Label("[" + cat + "]", _titleStyle);
                }
                if (GUILayout.Button(e.Name + "  —  " + e.Help, _btnStyle))
                    RunLine(e.Name);
            }
            GUILayout.EndScrollView();

            GUILayout.Label("Log", _titleStyle);
            var sb = new StringBuilder();
            for (var i = Mathf.Max(0, _log.Count - 24); i < _log.Count; i++)
                sb.AppendLine(_log[i]);
            GUILayout.TextArea(sb.ToString(), GUILayout.Height(140));

            if (GUILayout.Button("Close (F12 / ~)", _btnStyle, GUILayout.Height(36)))
                _open = false;

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 28));
        }

        void RunLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            Log("> " + line);
            DebugCmd.RunAsync(line, CancellationToken.None).ContinueWith(r =>
            {
                _lastResult = r.msg;
                Log(r.ok ? r.msg : "ERR " + r.msg);
            }).Forget();
        }

        void Log(string s)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {s}";
            _log.Add(line);
            while (_log.Count > MaxLog) _log.RemoveAt(0);
            Debug.Log("[DebugConsole] " + s);
        }
    }

    public sealed class DebugConsoleHook : MonoBehaviour
    {
        DebugConsole _console;

        public void Bind(DebugConsole c) => _console = c;

        void Update() => _console?.Tick();
        void OnGUI() => _console?.OnGUI();
    }

    /// <summary>Development / Editor 自动挂调试台。</summary>
    public static class DebugConsoleBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (!(Application.isEditor || Debug.isDebugBuild)) return;
            if (UnityEngine.Object.FindObjectOfType<DebugConsoleHook>() != null) return;

            CombatDebugCommands.Register();

            var go = new GameObject("[DebugConsole]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var hook = go.AddComponent<DebugConsoleHook>();
            var console = new DebugConsole();
            hook.Bind(console);
            Debug.Log("[DebugConsole] ready. F12 or ~ to toggle");
        }
    }
}
