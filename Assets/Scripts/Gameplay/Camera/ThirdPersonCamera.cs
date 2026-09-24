using UnityEngine;

namespace GameAct.Gameplay.Camera
{
    /// <summary>
    /// 渲染相机（RenderCamera）。
    ///
    /// 输入分两路，各走各的：
    /// - 【朝向】yaw / pitch 每帧直接读 LocalLookInput，1:1 跟手，不做任何平滑、不经过逻辑 tick。
    /// - 【位置】只平滑「焦点」（角色位置）的跟随，再在焦点上按 yaw/pitch 做刚性环绕。
    ///   这样鼠标转视角不会被位置平滑拖出迟滞/甩动，跑动时也只有跟随的轻微拖尾。
    ///
    /// 调用方（GameplayRunner）必须每个渲染帧把「已经过渲染插值」的角色位置传进来，
    /// 不能传 30Hz 逻辑 tick 的阶梯位置——否则再怎么平滑也会抖。
    /// </summary>
    [DefaultExecutionOrder(100)] // 在角色位姿写入之后
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Follow Target")]
        [SerializeField] Transform _followTarget; // 可选：直接跟 Transform

        [Header("Orbit")]
        [SerializeField] float _cameraDistance = 6f;
        [SerializeField] float _lookAtHeight = 1.5f;
        [Tooltip("焦点跟随平滑时间（秒）。0 = 刚性跟随。角色位置已插值时 0.05~0.1 即可。")]
        [SerializeField] float _followSmoothTime = 0.08f;
        [Tooltip("焦点与目标相距超过该值视为传送，直接瞬移。")]
        [SerializeField] float _teleportSnapDistance = 20f;

        [Header("Look Limits")]
        [SerializeField] float _minPitch = -10f;
        [SerializeField] float _maxPitch = 65f;
        [SerializeField] float _mouseDegPerPixel = 0.1f;

        [Header("Camera Setup")]
        [SerializeField] bool _forcePerspective = true;
        [SerializeField] float _fieldOfView = 60f;

        readonly LogicCamera _logic = new LogicCamera();
        UnityEngine.Camera _cam;

        Vector3 _targetPos;
        bool _hasExternalTarget;

        Vector3 _focus;
        Vector3 _focusVelocity;
        bool _focusInited;

        public LogicCamera Logic => _logic;

        public float Yaw => LocalLookInput.Yaw;
        public float Pitch => LocalLookInput.Pitch;

        /// <summary>视线水平前方 / 右方，瞄准、索敌用。</summary>
        public Vector3 PlanarForward => _logic.PlanarForward;
        public Vector3 PlanarRight => _logic.PlanarRight;

        public float CameraDistance
        {
            get => _cameraDistance;
            set { _cameraDistance = Mathf.Max(0.5f, value); _logic.Distance = _cameraDistance; }
        }

        public float LookAtHeight
        {
            get => _lookAtHeight;
            set { _lookAtHeight = value; _logic.LookAtHeight = value; }
        }

        public float SmoothTime
        {
            get => _followSmoothTime;
            set => _followSmoothTime = Mathf.Max(0f, value);
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

            SyncSettings();
        }

        void SyncSettings()
        {
            _logic.Distance = _cameraDistance;
            _logic.LookAtHeight = _lookAtHeight;
            LocalLookInput.MinPitch = _minPitch;
            LocalLookInput.MaxPitch = _maxPitch;
            LocalLookInput.MouseDegPerPixel = _mouseDegPerPixel;
        }

        /// <summary>
        /// 每个渲染帧调用：传入【渲染插值后】的角色位置。朝向不用传，相机自己读 LocalLookInput。
        /// </summary>
        public void SetTargetPosition(Vector3 position)
        {
            _targetPos = position;
            _hasExternalTarget = true;
        }

        [System.Obsolete("yaw 由 LocalLookInput 提供，该参数被忽略；请改用 SetTargetPosition。")]
        public void SetTargetPose(Vector3 position, float yawDegreesIgnored)
        {
            SetTargetPosition(position);
        }

        public void SetFollowTarget(Transform t)
        {
            _followTarget = t;
            _hasExternalTarget = false;
        }

        bool TryGetTarget(out Vector3 pos)
        {
            if (_hasExternalTarget) { pos = _targetPos; return true; }
            if (_followTarget != null) { pos = _followTarget.position; return true; }
            pos = default;
            return false;
        }

        void LateUpdate()
        {
            if (!TryGetTarget(out var target)) return;

            SyncSettings();

            if (!_focusInited)
            {
                _focus = target;
                _focusVelocity = Vector3.zero;
                _focusInited = true;
            }
            else if (_followSmoothTime <= 0f
                     || (target - _focus).sqrMagnitude > _teleportSnapDistance * _teleportSnapDistance)
            {
                _focus = target;
                _focusVelocity = Vector3.zero;
            }
            else
            {
                _focus = Vector3.SmoothDamp(_focus, target, ref _focusVelocity, _followSmoothTime);
            }

            Apply();
        }

        void Apply()
        {
            _logic.UpdateFromLook(_focus, LocalLookInput.Yaw, LocalLookInput.Pitch);
            transform.SetPositionAndRotation(_logic.Position, _logic.Rotation);
        }

        /// <summary>立刻跳到目标位置（开局、传送、切场景用）。</summary>
        public void SnapToTarget()
        {
            if (!TryGetTarget(out var target)) return;
            SyncSettings();
            _focus = target;
            _focusVelocity = Vector3.zero;
            _focusInited = true;
            Apply();
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
