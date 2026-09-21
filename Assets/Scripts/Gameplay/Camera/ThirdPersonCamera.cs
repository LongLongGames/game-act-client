using UnityEngine;

namespace GameAct.Gameplay.Camera
{
    /// <summary>
    /// 渲染相机（RenderCamera）。
    /// 每帧读取 LogicCamera 的理想目标，做 SmoothDamp + LookAt。
    /// 与 UnityExample 的 ClientPlayerView.LateUpdate 手感对齐。
    /// </summary>
    [DefaultExecutionOrder(100)] // 尽量在角色位姿更新之后
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Follow Target")]
        [SerializeField] Transform _followTarget; // 可选：直接跟 Transform
        [SerializeField] bool _useLogicCamera = true;

        [Header("Orbit (与 UnityExample 一致)")]
        [SerializeField] float _cameraDistance = 6f;
        [SerializeField] float _cameraHeight = 2.5f;
        [SerializeField] float _lookAtHeight = 1.5f;
        [SerializeField] float _smoothTime = 0.1f;

        [Header("Camera Setup")]
        [SerializeField] bool _forcePerspective = true;
        [SerializeField] float _fieldOfView = 60f;

        readonly LogicCamera _logic = new LogicCamera();
        Vector3 _dampVelocity;
        UnityEngine.Camera _cam;

        // 外部驱动接口（推荐）
        Vector3 _playerPos;
        float _playerYaw;
        bool _hasExternalPose;

        public LogicCamera Logic => _logic;

        public float CameraDistance
        {
            get => _cameraDistance;
            set { _cameraDistance = value; _logic.Distance = value; }
        }

        public float CameraHeight
        {
            get => _cameraHeight;
            set { _cameraHeight = value; _logic.Height = value; }
        }

        public float LookAtHeight
        {
            get => _lookAtHeight;
            set { _lookAtHeight = value; _logic.LookAtHeight = value; }
        }

        public float SmoothTime
        {
            get => _smoothTime;
            set => _smoothTime = Mathf.Max(0.01f, value);
        }

        void Awake()
        {
            _cam = GetComponent<UnityEngine.Camera>();
            if (_cam == null)
                _cam = gameObject.AddComponent<UnityEngine.Camera>();

            if (_forcePerspective)
            {
                _cam.orthographic = false;
                _cam.fieldOfView = _fieldOfView;
                _cam.nearClipPlane = 0.1f;
                _cam.farClipPlane = 200f;
            }

            SyncLogicSettings();
        }

        void SyncLogicSettings()
        {
            _logic.Distance = _cameraDistance;
            _logic.Height = _cameraHeight;
            _logic.LookAtHeight = _lookAtHeight;
        }

        /// <summary>
        /// 由 GameplayRunner / 本地玩家驱动：传入逻辑位姿。
        /// 优先于 FollowTarget。
        /// </summary>
        public void SetTargetPose(Vector3 position, float yawDegrees)
        {
            _playerPos = position;
            _playerYaw = yawDegrees;
            _hasExternalPose = true;
        }

        public void SetFollowTarget(Transform t)
        {
            _followTarget = t;
            _hasExternalPose = false;
        }

        void LateUpdate()
        {
            SyncLogicSettings();

            Vector3 pos;
            float yaw;

            if (_hasExternalPose)
            {
                pos = _playerPos;
                yaw = _playerYaw;
            }
            else if (_followTarget != null)
            {
                pos = _followTarget.position;
                yaw = _followTarget.eulerAngles.y;
            }
            else
            {
                return;
            }

            _logic.UpdateFromPlayer(pos, yaw);

            // 平滑位置（与 UnityExample SmoothDamp 一致）
            transform.position = Vector3.SmoothDamp(
                transform.position,
                _logic.Position,
                ref _dampVelocity,
                _smoothTime);

            // 朝向：直接 LookAt 逻辑注视点（示例也是如此，无额外平滑旋转）
            transform.LookAt(_logic.LookAtPoint);
        }

        /// <summary>立刻跳到目标位置（传送、切场景用）。</summary>
        public void SnapToTarget()
        {
            if (!_hasExternalPose && _followTarget == null) return;

            Vector3 pos = _hasExternalPose ? _playerPos : _followTarget.position;
            float yaw = _hasExternalPose ? _playerYaw : _followTarget.eulerAngles.y;

            _logic.UpdateFromPlayer(pos, yaw);
            transform.position = _logic.Position;
            transform.LookAt(_logic.LookAtPoint);
            _dampVelocity = Vector3.zero;
        }

        /// <summary>确保场景里有一个主相机并挂上本组件。</summary>
        public static ThirdPersonCamera EnsureMain()
        {
            var main = UnityEngine.Camera.main;
            if (main == null)
            {
                var go = new GameObject("Main Camera");
                main = go.AddComponent<UnityEngine.Camera>();
                go.tag = "MainCamera";
                go.AddComponent<AudioListener>();
            }

            var tpc = main.GetComponent<ThirdPersonCamera>();
            if (tpc == null)
                tpc = main.gameObject.AddComponent<ThirdPersonCamera>();

            return tpc;
        }
    }
}
