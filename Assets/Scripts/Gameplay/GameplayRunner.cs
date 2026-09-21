using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using GameAct.AppFlow;
using GameAct.Gameplay.Simulation;
using GameAct.Gameplay.Player;
using GameAct.Net;
using GameAct.Les;

namespace GameAct.Gameplay
{
    /// <summary>
    /// 玩家仍走 IGameSimulation；敌人走 LES（Solo 离线 / Host 联机权威）。
    /// </summary>
    [DefaultExecutionOrder(0)]
    public class GameplayRunner : MonoBehaviour
    {
        IGameSimulation _sim;
        INetSession _net;
        SessionMode _mode = SessionMode.Solo;
        PlayerView _localView;
        int _localId = -1;
        bool _started;
        string _levelSceneName = "Map1";
        LesAuthoritySession _lesSolo;

        public IGameSimulation Simulation => _sim;
        public string LevelSceneName => _levelSceneName;
        public SessionMode Mode => _mode;
        public bool IsStarted => _started;

        public void StartSession(INetSession net, string levelSceneName, SessionMode mode)
        {
            if (_started)
            {
                Debug.LogWarning("[Gameplay] StartSession ignored: already started. Call StopSession first.");
                return;
            }

            _net = net;
            _mode = mode;
            _levelSceneName = string.IsNullOrEmpty(levelSceneName) ? "Map1" : levelSceneName;

            EnsureLevelActive(_levelSceneName);
            _sim = CreateSimulation(mode, net);

            var spawnPos = SnapToGround(FindSpawnPosition());
            _localId = _sim.SpawnPlayer(spawnPos, isLocal: true);

            _localView = CreatePlayerView(_localId, isLocal: true);
            MoveToLevelScene(_localView.gameObject, _levelSceneName);
            _localView.transform.position = spawnPos;
            spawnPos = SnapToGround(_localView.transform.position);
            _localView.transform.position = spawnPos;

            if (_sim is LocalSimulation local)
                local.BindCharacterController(_localId, _localView.CharacterController);
            else if (_sim is ClientSimulation client)
                client.BindCharacterController(_localId, _localView.CharacterController);

            _localView.EnableController();

            // 敌人
            if (mode == SessionMode.Solo)
            {
                _lesSolo = new LesAuthoritySession();
                _lesSolo.Start(spawnPos, _levelSceneName, LesAuthoritySession.DefaultEnemyCount);
            }
            else if (mode == SessionMode.Host && net is LesNetworkHub hub)
            {
                hub.SpawnEnemiesAround(spawnPos, _levelSceneName, LesNetworkHub.DefaultEnemyCount);
            }
            // Client：敌人由 LES 快照构造，LesNetworkHub.Poll → SyncEnemyViews

            _started = true;
            Debug.Log($"[Gameplay] mode={mode} pos={spawnPos} net={net?.Role}");
        }

        public void StartSession(INetSession net, string levelSceneName = "Map1")
        {
            var mode = SessionMode.Solo;
            if (net != null && net.IsConnected)
                mode = net.Role == NetRole.Client ? SessionMode.Client : SessionMode.Host;
            StartSession(net, levelSceneName, mode);
        }

        public void StopSession()
        {
            _lesSolo?.Stop();
            _lesSolo = null;

            if (_sim != null && _localId >= 0)
                _sim.Despawn(_localId);
            _localId = -1;

            if (_localView != null)
            {
                Destroy(_localView.gameObject);
                _localView = null;
            }

            _sim = null;
            _net = null;
            _mode = SessionMode.Solo;
            _started = false;
        }

        static IGameSimulation CreateSimulation(SessionMode mode, INetSession net)
        {
            switch (mode)
            {
                case SessionMode.Solo:
                case SessionMode.Host:
                    return new LocalSimulation();
                case SessionMode.Client:
                    if (net == null || !net.IsConnected || net.Role != NetRole.Client)
                        throw new System.InvalidOperationException("Client 未连接");
                    return new ClientSimulation();
                default:
                    return new LocalSimulation();
            }
        }

        public static void EnsureLevelActive(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded) return;
            if (SceneManager.GetActiveScene() != scene)
                SceneManager.SetActiveScene(scene);
        }

        public static void MoveToLevelScene(GameObject go, string sceneName)
        {
            if (go == null) return;
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded) return;
            if (go.scene == scene) return;
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        static Vector3 FindSpawnPosition()
        {
            var t = GameObject.Find("PlayerSpawn");
            if (t != null) return t.transform.position;
            var origin = new Vector3(0f, 50f, 0f);
            if (Physics.Raycast(origin, Vector3.down, out var hit, 200f))
                return hit.point;
            return new Vector3(0f, 0.05f, 0f);
        }

        static Vector3 SnapToGround(Vector3 pos)
        {
            var origin = pos + Vector3.up * 5f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.02f;
            origin = pos + Vector3.up * 50f;
            if (Physics.Raycast(origin, Vector3.down, out hit, 100f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.02f;
            return new Vector3(pos.x, Mathf.Max(pos.y, 0.05f), pos.z);
        }

        static PlayerView CreatePlayerView(int entityId, bool isLocal)
        {
            var go = new GameObject("PlayerView_Local");
            var view = go.AddComponent<PlayerView>();
            view.Setup(entityId, isLocal);
            return view;
        }

        void Update()
        {
            if (!_started || _sim == null) return;

            float dt = Time.deltaTime;
            var input = ReadInput();
            if (_localId >= 0)
                _sim.ApplyInput(_localId, input);
            _sim.Tick(dt);
            if (_localView != null && _sim.TryGetPose(_localId, out var pose))
                _localView.ApplyPose(pose);

            // Solo 离线 LES；Host/Client 由 NetRunner → LesNetworkHub.Poll 驱动
            _lesSolo?.Tick();
        }

        static PlayerInputCmd ReadInput()
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
            return new PlayerInputCmd
            {
                Move = new Vector2(x, y),
                Sprint = sprint,
                Jump = jump,
                Sequence = 0
            };
        }

        void OnDestroy() => StopSession();
    }
}
