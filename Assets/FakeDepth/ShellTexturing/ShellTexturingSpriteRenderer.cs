using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[AddComponentMenu("Rendering/Fake Depth/Shell Texturing Sprite Renderer")]
[DisallowMultipleComponent]
public sealed class ShellTexturingSpriteRenderer : MonoBehaviour
{
    private const int MaxShellCount = 128;

    [Tooltip("Sprite whose tight mesh and texture are reused by every shell instance.")]
    [SerializeField] private Sprite m_Sprite;

    [Tooltip("Color multiplied with every shell. Alpha is also multiplied by the per-shell opacity curve.")]
    [SerializeField] private Color m_Tint = Color.black;

    [SerializeField] private bool m_FlipX;
    [SerializeField] private bool m_FlipY;

    [Tooltip("Number of GPU instances drawn in one call. More shells produce smoother depth but increase transparent overdraw.")]
    [Range(1, MaxShellCount)]
    [SerializeField] private int m_ShellCount = 20;

    [Tooltip("Total local-space Z extrusion. For a normal 2D camera on negative Z, use a negative value to extend toward the camera.")]
    [SerializeField] private float m_TotalDepth = -1.0f;

    [Tooltip("Controls shell placement through the extrusion. X is normalized shell index and Y is normalized depth.")]
    [SerializeField] private AnimationCurve m_DepthDistribution = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Tooltip("Controls shell alpha through the extrusion. X is normalized depth and Y is the opacity multiplied with the sprite and Tint alpha.")]
    [SerializeField] private AnimationCurve m_OpacityByDepth = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Tooltip("Discards sprite pixels at or below this texture alpha. Increase it to reduce faint transparent overdraw around the silhouette.")]
    [Range(0.0f, 1.0f)]
    [SerializeField] private float m_AlphaClipThreshold = 0.001f;

    [Tooltip("Material that uses the Shell Texturing Sprite Unlit shader and has GPU Instancing enabled.")]
    [SerializeField] private Material m_Material;

    [Tooltip("Fine-tunes ordering against other transparent draws using the same render queue. Shell order inside this draw is handled automatically for a camera on negative local Z.")]
    [SerializeField] private int m_RendererPriority;

    private struct ShellInstanceData
    {
        // RenderMeshInstanced requires objectToWorld as the first field.
        public Matrix4x4 objectToWorld;
    }

    private struct ShellSample
    {
        public float Depth;
        public float Opacity;
    }

    private readonly ShellInstanceData[] _instances = new ShellInstanceData[MaxShellCount];
    private readonly ShellSample[] _shellSamples = new ShellSample[MaxShellCount];
    private readonly float[] _shellDepths = new float[MaxShellCount];
    private readonly float[] _shellOpacities = new float[MaxShellCount];

    private Mesh _mesh;
    private Sprite _meshSprite;
    private bool _meshFlipX;
    private bool _meshFlipY;
    private bool _shellDataDirty = true;
    private bool _reportedMissingMaterial;
    private bool _reportedInstancingUnavailable;
    private MaterialPropertyBlock _propertyBlock;

    private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
    private static readonly int TintId = Shader.PropertyToID("_Tint");
    private static readonly int AlphaClipThresholdId = Shader.PropertyToID("_AlphaClipThreshold");
    private static readonly int ShellOpacityId = Shader.PropertyToID("_ShellOpacity");

    private void OnEnable()
    {
        _shellDataDirty = true;
        EnsurePropertyBlock();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        m_ShellCount = Mathf.Clamp(m_ShellCount, 1, MaxShellCount);
        m_AlphaClipThreshold = Mathf.Clamp01(m_AlphaClipThreshold);
        _shellDataDirty = true;
        _reportedMissingMaterial = false;
        _reportedInstancingUnavailable = false;
    }
#endif

    private void LateUpdate()
    {
        RenderShells();
    }

    private void OnDestroy()
    {
        DestroyGeneratedMesh();
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        _shellDataDirty = true;
        DestroyGeneratedMesh();
        EnsureMesh();
        BuildShellData();
    }

    private void RenderShells()
    {
        if (m_Sprite == null)
            return;

        if (m_Material == null)
        {
            if (!_reportedMissingMaterial)
            {
                Debug.LogWarning($"'{name}' needs a Shell Texturing material.", this);
                _reportedMissingMaterial = true;
            }

            return;
        }

        _reportedMissingMaterial = false;

        if (!SystemInfo.supportsInstancing || !m_Material.enableInstancing)
        {
            if (!_reportedInstancingUnavailable)
            {
                string reason = SystemInfo.supportsInstancing ? "GPU Instancing is disabled on its material" : "GPU Instancing is unavailable on this platform";
                Debug.LogWarning($"'{name}' cannot render shell instances because {reason}.", this);
                _reportedInstancingUnavailable = true;
            }

            return;
        }

        _reportedInstancingUnavailable = false;
        EnsureMesh();
        if (_mesh == null)
            return;

        if (_shellDataDirty)
            BuildShellData();

        UpdateInstanceTransforms();
        UpdateMaterialProperties();

        RenderParams renderParams = new(m_Material)
        {
            layer = gameObject.layer,
            matProps = _propertyBlock,
            worldBounds = CalculateWorldBounds(),
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            lightProbeUsage = LightProbeUsage.Off,
            reflectionProbeUsage = ReflectionProbeUsage.Off,
            motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,
            rendererPriority = m_RendererPriority
        };

        Graphics.RenderMeshInstanced(renderParams, _mesh, 0, _instances, m_ShellCount);
    }

    private void EnsureMesh()
    {
        if (_mesh != null && _meshSprite == m_Sprite && _meshFlipX == m_FlipX && _meshFlipY == m_FlipY)
            return;

        DestroyGeneratedMesh();
        if (m_Sprite == null)
            return;

        Vector2[] sourceVertices = m_Sprite.vertices;
        Vector2[] sourceUVs = m_Sprite.uv;
        ushort[] sourceTriangles = m_Sprite.triangles;

        if (sourceVertices == null || sourceVertices.Length == 0 || sourceTriangles == null || sourceTriangles.Length == 0)
            return;

        var vertices = new Vector3[sourceVertices.Length];
        var triangles = new int[sourceTriangles.Length];

        for (int vertexIndex = 0; vertexIndex < sourceVertices.Length; ++vertexIndex)
        {
            Vector2 sourceVertex = sourceVertices[vertexIndex];
            vertices[vertexIndex] = new Vector3(m_FlipX ? -sourceVertex.x : sourceVertex.x, m_FlipY ? -sourceVertex.y : sourceVertex.y, 0f);
        }

        for (int triangleIndex = 0; triangleIndex < sourceTriangles.Length; ++triangleIndex)
            triangles[triangleIndex] = sourceTriangles[triangleIndex];

        _mesh = new Mesh
        {
            name = $"{name}_ShellTexturingSprite",
            hideFlags = HideFlags.HideAndDontSave,
            vertices = vertices,
            uv = sourceUVs,
            triangles = triangles
        };

        _mesh.RecalculateBounds();
        _meshSprite = m_Sprite;
        _meshFlipX = m_FlipX;
        _meshFlipY = m_FlipY;
    }

    private void BuildShellData()
    {
        for (int shellIndex = 0; shellIndex < MaxShellCount; ++shellIndex)
        {
            _shellSamples[shellIndex] = default;
            _shellDepths[shellIndex] = 0f;
            _shellOpacities[shellIndex] = 0f;
        }

        for (int shellIndex = 0; shellIndex < m_ShellCount; ++shellIndex)
        {
            float normalizedShellIndex = m_ShellCount <= 1
                ? 0f
                : shellIndex / (float)(m_ShellCount - 1);
            float normalizedDepth = Mathf.Clamp01(m_DepthDistribution.Evaluate(normalizedShellIndex));

            _shellSamples[shellIndex] = new ShellSample
            {
                Depth = m_TotalDepth * normalizedDepth,
                Opacity = Mathf.Clamp01(m_OpacityByDepth.Evaluate(normalizedDepth))
            };
        }

        // Transparent shells must be submitted back-to-front. Descending local Z
        // is correct for the documented camera convention (camera on negative Z).
        for (int shellIndex = 1; shellIndex < m_ShellCount; ++shellIndex)
        {
            ShellSample sample = _shellSamples[shellIndex];
            int insertionIndex = shellIndex - 1;

            while (insertionIndex >= 0 && _shellSamples[insertionIndex].Depth < sample.Depth)
            {
                _shellSamples[insertionIndex + 1] = _shellSamples[insertionIndex];
                --insertionIndex;
            }

            _shellSamples[insertionIndex + 1] = sample;
        }

        for (int shellIndex = 0; shellIndex < m_ShellCount; ++shellIndex)
        {
            _shellDepths[shellIndex] = _shellSamples[shellIndex].Depth;
            _shellOpacities[shellIndex] = _shellSamples[shellIndex].Opacity;
        }

        _shellDataDirty = false;
    }

    private void UpdateInstanceTransforms()
    {
        Matrix4x4 localToWorld = transform.localToWorldMatrix;
        for (int shellIndex = 0; shellIndex < m_ShellCount; ++shellIndex)
        {
            _instances[shellIndex].objectToWorld = localToWorld * Matrix4x4.Translate(new Vector3(0f, 0f, _shellDepths[shellIndex]));
        }
    }

    private void UpdateMaterialProperties()
    {
        EnsurePropertyBlock();
        _propertyBlock.Clear();
        _propertyBlock.SetTexture(MainTextureId, m_Sprite.texture);
        _propertyBlock.SetColor(TintId, m_Tint);
        _propertyBlock.SetFloat(AlphaClipThresholdId, m_AlphaClipThreshold);
        _propertyBlock.SetFloatArray(ShellOpacityId, _shellOpacities);
    }

    private void EnsurePropertyBlock()
    {
        _propertyBlock ??= new MaterialPropertyBlock();
    }

    private Bounds CalculateWorldBounds()
    {
        Bounds meshBounds = _mesh.bounds;
        Bounds worldBounds = TransformBounds(meshBounds, _instances[0].objectToWorld);

        for (int shellIndex = 1; shellIndex < m_ShellCount; ++shellIndex)
            worldBounds.Encapsulate(TransformBounds(meshBounds, _instances[shellIndex].objectToWorld));

        return worldBounds;
    }

    private static Bounds TransformBounds(Bounds localBounds, Matrix4x4 localToWorld)
    {
        Vector3 localExtents = localBounds.extents;
        Vector3 worldCenter = localToWorld.MultiplyPoint3x4(localBounds.center);
        Vector3 worldExtents = new(
            Mathf.Abs(localToWorld.m00) * localExtents.x + Mathf.Abs(localToWorld.m01) * localExtents.y + Mathf.Abs(localToWorld.m02) * localExtents.z,
            Mathf.Abs(localToWorld.m10) * localExtents.x + Mathf.Abs(localToWorld.m11) * localExtents.y + Mathf.Abs(localToWorld.m12) * localExtents.z,
            Mathf.Abs(localToWorld.m20) * localExtents.x + Mathf.Abs(localToWorld.m21) * localExtents.y + Mathf.Abs(localToWorld.m22) * localExtents.z
        );

        return new Bounds(worldCenter, worldExtents * 2f);
    }

    private void DestroyGeneratedMesh()
    {
        if (_mesh == null)
            return;

        if (Application.isPlaying)
            Destroy(_mesh);
        else
            DestroyImmediate(_mesh);

        _mesh = null;
        _meshSprite = null;
    }
}
