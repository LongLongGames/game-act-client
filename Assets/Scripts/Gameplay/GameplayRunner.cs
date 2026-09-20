using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using GameAct.Gameplay.Simulation;
using GameAct.Gameplay.Player;
using GameAct.Net;

namespace GameAct.Gameplay
{
    /// <summary>
    /// 会话级 Runner：输入 → 模拟 → View。
    /// 实体进关卡场景；玩家使用 Y_Bot + 贴地后再开 CharacterController。
    /// </summary>
    public class GameplayRunner : MonoBehaviour
    {
        IGameSimulation _sim;
        INetSession _net;
        PlayerView _localView;
        int _localId = -1;
        bool _started;
        string _levelSceneName = "Map1";

        public IGameSimulation Simulation => _sim;
        public string LevelSceneName => _levelSceneName;

        public void StartSession(INetSession net, string levelSceneName = "Map1")
        {
            if (_started) return;
            _started = true;
            _net = net;
            _levelSceneName = string.IsNullOrEmpty(levelSceneName) ? "Map1" : levelSceneName;

            EnsureLevelActive(_levelSceneName);

            bool isClientOnly = net != null && net.IsConnected && net.Role == NetRole.Client;
            _sim = isClientOnly ? (IGameSimulation)new ClientSimulation() : new LocalSimulation();

            var spawnPos = SnapToGround(FindSpawnPosition());

            _localId = _sim.SpawnPlayer(spawnPos, isLocal: true);
            _localView = CreatePlayerView(_localId, isLocal: true);
            MoveToLevelScene(_localView.gameObject, _levelSceneName);

            // 贴地后再写坐标，最后才启用 CC（防止生成帧重力穿地）
            spawnPos = SnapToGround(spawnPos);
            _localView.transform.position = spawnPos;
            // 同步模拟层初始坐标：重新绑定前再 Spawn 一次对齐
            _sim.Despawn(_localId);
            _localId = _sim.SpawnPlayer(spawnPos, isLocal: true);
            _localView.EntityId = _localId;

            if (_sim is LocalSimulation local)
                local.BindCharacterController(_localId, _localView.CharacterController);
            else if (_sim is ClientSimulation client)
                client.BindCharacterController(_localId, _localView.CharacterController);

            _localView.EnableController();

            Debug.Log($"[Gameplay] Y_Bot localId={_localId} pos={spawnPos} level={_levelSceneName} " +
                      $"scene={_localView.gameObject.scene.name} role={net?.Role}");
        }

        public void PrepareSpawn() => EnsureLevelActive(_levelSceneName);

        public void AttachToLevel(GameObject go)
        {
            if (go == null) return;
            MoveToLevelScene(go, _levelSceneName);
        }

        public static void EnsureLevelActive(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning($"[Gameplay] Level scene not loaded: {sceneName}");
                return;
            }
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

        /// <summary>
        /// 从点上方往下射线贴地。失败则略抬高，避免生成在网格内。
        /// CharacterController 脚底在 transform.position，center.y=0.9 时胶囊底≈ position.y。
        /// </summary>
        static Vector3 SnapToGround(Vector3 pos)
        {
            var origin = pos + Vector3.up * 5f;
            // 多射线防薄碰撞漏检
            if (Physics.Raycast(origin, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.02f;

            origin = pos + Vector3.up * 50f;
            if (Physics.Raycast(origin, Vector3.down, out hit, 100f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.02f;

            // 没碰到碰撞体：不要从高空扔下去，保持原 y 或微抬
            return new Vector3(pos.x, Mathf.Max(pos.y, 0.05f), pos.z);
        }

        static PlayerView CreatePlayerView(int entityId, bool isLocal)
        {
            var go = new GameObject();
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
                if (stick.sqrMagnitude > 0.01f)
                {
                    x = stick.x;
                    y = stick.y;
                }
                if (pad.leftShoulder.isPressed || pad.leftStickButton.isPressed)
                    sprint = true;
                if (pad.buttonSouth.wasPressedThisFrame)
                    jump = true;
            }

            return new PlayerInputCmd
            {
                Move = new Vector2(x, y),
                Sprint = sprint,
                Jump = jump,
                Sequence = 0
            };
        }

        void OnDestroy()
        {
            if (_sim != null && _localId >= 0)
                _sim.Despawn(_localId);
        }
    }
}
