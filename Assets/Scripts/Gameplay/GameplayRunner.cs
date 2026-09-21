using UnityEngine;
using UnityEngine.SceneManagement;
using GameAct.AppFlow;
using GameAct.Gameplay.Player;
using GameAct.Gameplay.Camera;
using GameAct.Net;
using GameAct.Les;
using GameAct.Les.Shared;

namespace GameAct.Gameplay
{
    /// <summary>
    /// LES 主路径：Input → ActPlayer 积分 → View 只跟位姿。
    /// 相机跟随「视线 Yaw」（鼠标），模型用身体 Yaw（朝移动方向）。
    /// </summary>
    [DefaultExecutionOrder(0)]
    public class GameplayRunner : MonoBehaviour
    {
        INetSession _net;
        SessionMode _mode = SessionMode.Solo;
        PlayerView _localView;
        ThirdPersonCamera _camera;
        bool _started;
        string _levelSceneName = "Map1";
        LesAuthoritySession _lesSolo;
        ActPlayer _lesLocalPlayer;

        public SessionMode Mode => _mode;
        public bool IsStarted => _started;
        public ActPlayer LesLocalPlayer => _lesLocalPlayer;
        public ThirdPersonCamera Camera => _camera;

        public void StartSession(INetSession net, string levelSceneName, SessionMode mode)
        {
            if (_started)
            {
                Debug.LogWarning("[Gameplay] already started");
                return;
            }

            _net = net;
            _mode = mode;
            _levelSceneName = string.IsNullOrEmpty(levelSceneName) ? "Map1" : levelSceneName;
            EnsureLevelActive(_levelSceneName);

            var spawnPos = SnapToGround(FindSpawnPosition());

            if (mode == SessionMode.Solo)
            {
                _lesSolo = new LesAuthoritySession();
                _lesSolo.Start(spawnPos, _levelSceneName);
                _lesLocalPlayer = _lesSolo.LocalPlayer;
            }
            else if (mode == SessionMode.Host && net is LesNetworkHub hub)
            {
                _lesLocalPlayer = hub.SpawnLocalPlayer(spawnPos);
                hub.SpawnMonstersAround(spawnPos, _levelSceneName);
            }
            else if (mode == SessionMode.Client && net is LesNetworkHub)
            {
                _lesLocalPlayer = null;
            }
            else
            {
                throw new System.InvalidOperationException("无法启动 LES 会话: mode=" + mode);
            }

            _localView = CreatePlayerView(0, isLocal: true);
            MoveToLevelScene(_localView.gameObject, _levelSceneName);
            if (_lesLocalPlayer != null)
                _localView.ApplyPose(_lesLocalPlayer.Position, _lesLocalPlayer.Yaw, 0f);
            else
                _localView.ApplyPose(spawnPos, 0f, 0f);

            _camera = ThirdPersonCamera.EnsureMain();
            if (_lesLocalPlayer != null)
            {
                _camera.SetTargetPose(_lesLocalPlayer.Position, _lesLocalPlayer.LookYaw);
                _camera.SnapToTarget();
            }

            _started = true;
            Debug.Log($"[Gameplay] LES session mode={mode} spawn={spawnPos} (body turns to move dir)");
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
            _lesLocalPlayer = null;
            if (_localView != null)
            {
                Destroy(_localView.gameObject);
                _localView = null;
            }
            _camera = null;
            _net = null;
            _mode = SessionMode.Solo;
            _started = false;
        }

        void Update()
        {
            if (!_started) return;

            _lesSolo?.Tick();

            if (_lesLocalPlayer == null && _mode == SessionMode.Client && _net is LesNetworkHub hub)
                _lesLocalPlayer = FindLocalClientPlayer(hub);

            if (_lesLocalPlayer != null && !_lesLocalPlayer.IsDestroyed && _localView != null)
            {
                var pos = _lesLocalPlayer.InterpolatedPosition;
                // 模型：身体朝向（会转向移动方向）
                var bodyYaw = _lesLocalPlayer.InterpolatedYaw;
                var v = _lesLocalPlayer.Velocity;
                float speedXZ = new Vector2(v.x, v.z).magnitude;
                _localView.ApplyPose(pos, bodyYaw, speedXZ);

                // 相机：视线 Yaw（鼠标），按 A 转身时镜头不会硬甩
                if (_camera != null)
                    _camera.SetTargetPose(pos, _lesLocalPlayer.LookYaw);
            }
        }

        static ActPlayer FindLocalClientPlayer(LesNetworkHub hub)
        {
            var em = hub.ClientEm;
            if (em == null) return null;
            foreach (var p in em.GetEntities<ActPlayer>())
            {
                if (p != null && !p.IsDestroyed && p.IsLocalControlled)
                    return p;
            }
            return null;
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
            var origin = pos + Vector3.up * 50f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 100f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.05f;
            origin = pos + Vector3.up * 5f;
            if (Physics.Raycast(origin, Vector3.down, out hit, 20f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.05f;
            return new Vector3(pos.x, Mathf.Max(pos.y, 0.05f), pos.z);
        }

        static PlayerView CreatePlayerView(int entityId, bool isLocal)
        {
            var go = new GameObject("PlayerView_Local");
            var view = go.AddComponent<PlayerView>();
            view.Setup(entityId, isLocal);
            return view;
        }

        void OnDestroy() => StopSession();
    }
}
