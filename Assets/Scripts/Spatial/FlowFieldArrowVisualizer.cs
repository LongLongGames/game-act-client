using UnityEngine;
using UnityEngine.InputSystem;

namespace GameAct.Spatial
{
    /// <summary>
    /// Flow Field 箭头阵列可视化。
    /// 用 Graphics.DrawMeshInstanced 画网格箭头（不用 ParticleSystem.rotation3D，朝向可靠）。
    /// RefreshHz 控制采样刷新；每帧提交绘制，箭头方向随场数据更新。
    /// </summary>
    public class FlowFieldArrowVisualizer : MonoBehaviour
    {
        const int MaxInstancesPerBatch = 1023; // DrawMeshInstanced 上限

        [Header("Refresh")]
        [Tooltip("从 FlowField 重新采样的频率（Hz）。1 = 每秒采一次。绘制每帧都会提交。")]
        [Range(0.1f, 60f)]
        public float RefreshHz = 1f;

        [Header("Grid sampling")]
        [Range(1, 8)]
        public int CellStride = 2;

        public float YOffset = 0.25f;

        [Tooltip("箭头世界长度（米）")]
        public float ArrowLength = 1.1f;

        [Tooltip("箭头宽度相对长度的比例")]
        [Range(0.15f, 0.6f)]
        public float ArrowWidthScale = 0.35f;

        [Header("Color")]
        public Color ArrowColor = new Color(0.15f, 0.9f, 1f, 0.9f);
        public Color NearTargetColor = new Color(0.4f, 1f, 0.5f, 0.95f);
        public bool ColorByCost = true;
        public bool HideBlocked = true;
        public bool HideZeroFlow = true;

        [Header("Toggle")]
        public bool Visible = true;
        public Key ToggleKey = Key.F3;

        Mesh _arrowMesh;
        Material _mat;
        Matrix4x4[] _matrices;
        Vector4[] _colors;
        MaterialPropertyBlock _mpb;
        int _instanceCount;
        float _sampleTimer;
        Vector3 _lastSampledTarget;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            _arrowMesh = BuildArrowMesh();
            _mat = CreateMaterial();
            _mpb = new MaterialPropertyBlock();
            _matrices = new Matrix4x4[MaxInstancesPerBatch];
            _colors = new Vector4[MaxInstancesPerBatch];
            _sampleTimer = 0f; // 首帧立刻采一次
        }

        void OnDestroy()
        {
            if (_arrowMesh != null) Destroy(_arrowMesh);
            if (_mat != null) Destroy(_mat);
        }

        void Update()
        {
            if (ToggleKey != Key.None)
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    var ctrl = kb[ToggleKey];
                    if (ctrl != null && ctrl.wasPressedThisFrame)
                        Visible = !Visible;
                }
            }

            if (!Visible)
            {
                _instanceCount = 0;
                return;
            }

            float interval = 1f / Mathf.Max(0.05f, RefreshHz);
            _sampleTimer -= Time.deltaTime;

            // 目标移动也强制重采（避免场已重建但可视化还在等 Hz）
            var field = FlowFieldService.Field;
            bool targetMoved = false;
            if (field != null && field.HasField)
            {
                Vector3 t = field.LastTarget;
                if ((t - _lastSampledTarget).sqrMagnitude > 0.25f)
                    targetMoved = true;
            }

            if (_sampleTimer <= 0f || targetMoved || _instanceCount == 0)
            {
                _sampleTimer = interval;
                RebuildInstances();
            }
        }

        Matrix4x4[] _batchScratch;

        void LateUpdate()
        {
            if (!Visible || _instanceCount <= 0 || _arrowMesh == null || _mat == null)
                return;

            if (_batchScratch == null || _batchScratch.Length < MaxInstancesPerBatch)
                _batchScratch = new Matrix4x4[MaxInstancesPerBatch];

            int drawn = 0;
            while (drawn < _instanceCount)
            {
                int batch = Mathf.Min(MaxInstancesPerBatch, _instanceCount - drawn);
                for (int i = 0; i < batch; i++)
                    _batchScratch[i] = _matrices[drawn + i];

                Graphics.DrawMeshInstanced(
                    _arrowMesh,
                    0,
                    _mat,
                    _batchScratch,
                    batch,
                    _mpb,
                    UnityEngine.Rendering.ShadowCastingMode.Off,
                    false,
                    gameObject.layer);

                drawn += batch;
            }
        }

        void RebuildInstances()
        {
            _instanceCount = 0;
            var field = FlowFieldService.Field;
            if (field == null || !field.HasField)
                return;

            _lastSampledTarget = field.LastTarget;

            int stride = Mathf.Max(1, CellStride);
            int w = field.Width;
            int h = field.Height;
            float len = Mathf.Max(0.2f, ArrowLength);
            float y = YOffset;

            float maxCost = 1f;
            if (ColorByCost)
            {
                for (int cz = 0; cz < h; cz += stride)
                for (int cx = 0; cx < w; cx += stride)
                {
                    if (!field.TryGetCell(cx, cz, out _, out float integ, out bool blocked))
                        continue;
                    if (blocked || integ >= FlowField.Unreachable * 0.5f) continue;
                    if (integ > maxCost) maxCost = integ;
                }
            }

            // 需要可扩容
            int estimate = ((w + stride - 1) / stride) * ((h + stride - 1) / stride);
            int cap = Mathf.Max(estimate, 64);
            if (_matrices == null || _matrices.Length < cap)
            {
                _matrices = new Matrix4x4[cap];
                _colors = new Vector4[cap];
            }

            for (int cz = 0; cz < h; cz += stride)
            {
                for (int cx = 0; cx < w; cx += stride)
                {
                    if (_instanceCount >= _matrices.Length)
                        break;

                    if (!field.TryGetCell(cx, cz, out var flow, out float integ, out bool blocked))
                        continue;

                    if (blocked)
                    {
                        if (HideBlocked) continue;
                        // 画小竖点表示阻挡
                        Vector3 c = field.CellCenter(cx, cz);
                        c.y = y;
                        _matrices[_instanceCount++] = Matrix4x4.TRS(
                            c, Quaternion.identity, new Vector3(0.15f, 0.15f, 0.15f));
                        continue;
                    }

                    if (flow.sqrMagnitude < 1e-6f)
                    {
                        if (HideZeroFlow) continue;
                        Vector3 c0 = field.CellCenter(cx, cz);
                        c0.y = y;
                        _matrices[_instanceCount++] = Matrix4x4.TRS(
                            c0, Quaternion.identity, new Vector3(0.2f, 0.2f, 0.2f));
                        continue;
                    }

                    // flow: Vector2 (x, z) → 世界方向
                    Vector3 dir = new Vector3(flow.x, 0f, flow.y);
                    if (dir.sqrMagnitude < 1e-8f) continue;
                    dir.Normalize();

                    Vector3 pos = field.CellCenter(cx, cz);
                    pos.y = y;

                    // 箭头 mesh 默认朝 +Z，用 LookRotation 对齐
                    Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

                    float scaleMul = 1f;
                    if (ColorByCost && integ < FlowField.Unreachable * 0.5f)
                        scaleMul = Mathf.Lerp(0.75f, 1.2f, 1f - Mathf.Clamp01(integ / maxCost));

                    Vector3 scale = new Vector3(
                        len * ArrowWidthScale * scaleMul,
                        1f,
                        len * scaleMul);

                    _matrices[_instanceCount++] = Matrix4x4.TRS(pos, rot, scale);
                }
            }

            // 材质颜色
            if (_mat != null)
            {
                _mat.color = ArrowColor;
                if (_mat.HasProperty(BaseColorId))
                    _mat.SetColor(BaseColorId, ArrowColor);
                if (_mat.HasProperty(ColorId))
                    _mat.SetColor(ColorId, ArrowColor);
            }
            _mpb.Clear();
            _mpb.SetColor(ColorId, ArrowColor);
            if (_mat != null && _mat.HasProperty(BaseColorId))
                _mpb.SetColor(BaseColorId, ArrowColor);
        }

        static Material CreateMaterial()
        {
            // URP Unlit 优先
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Standard");
            var mat = new Material(sh);
            mat.enableInstancing = true;
            mat.color = new Color(0.15f, 0.9f, 1f, 0.9f);
            if (mat.HasProperty(BaseColorId))
                mat.SetColor(BaseColorId, mat.color);
            // 透明
            if (mat.HasProperty("_Surface"))
                mat.SetFloat("_Surface", 1f);
            return mat;
        }

        /// <summary>箭头 mesh：沿 +Z，原点在中心。</summary>
        static Mesh BuildArrowMesh()
        {
            var mesh = new Mesh { name = "FlowFieldArrow" };

            const float halfLen = 0.5f;
            const float shaftW = 0.1f;
            const float headW = 0.28f;
            const float headLen = 0.32f;
            float shaftEnd = halfLen - headLen;

            var verts = new Vector3[]
            {
                new Vector3(-shaftW, 0f, -halfLen),
                new Vector3( shaftW, 0f, -halfLen),
                new Vector3( shaftW, 0f,  shaftEnd),
                new Vector3(-shaftW, 0f,  shaftEnd),
                new Vector3(-headW, 0f, shaftEnd),
                new Vector3( headW, 0f, shaftEnd),
                new Vector3( 0f,    0f,  halfLen),
            };

            var tris = new int[]
            {
                0, 2, 1, 0, 3, 2,
                4, 6, 5,
                // 双面
                0, 1, 2, 0, 2, 3,
                4, 5, 6
            };

            var norms = new Vector3[verts.Length];
            for (int i = 0; i < norms.Length; i++)
                norms[i] = Vector3.up;

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.normals = norms;
            mesh.RecalculateBounds();
            return mesh;
        }

        public static FlowFieldArrowVisualizer Create(Transform parent = null, float hz = 1f)
        {
            var go = new GameObject("FlowFieldArrowVisualizer");
            if (parent != null)
                go.transform.SetParent(parent, false);
            var viz = go.AddComponent<FlowFieldArrowVisualizer>();
            viz.RefreshHz = hz;
            return viz;
        }
    }
}
