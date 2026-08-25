using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[ExecuteAlways]
[AddComponentMenu("Rendering/Virtual Depth/God Ray Renderer")]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(VirtualDepthSpriteRenderer))]
[MovedFrom(true, null, null, "VirtualDepthGodRay")]
public sealed class VirtualDepthGodRayRenderer : MonoBehaviour
{
    public enum LayerOffsetMode
    {
        Manual,
        SourceForward,
        AwayFromSource
    }

    private const float MinimumTrackedDirectionZ = 0.0001f;

    [Tooltip("Per-renderer multiplier for the accumulated God Ray light.")]
    [Min(0f)] [SerializeField] private float m_Intensity = 1f;

    [Tooltip("How the local-space layer offset per unit depth is calculated.")]
    [SerializeField] private LayerOffsetMode m_LayerOffsetMode = LayerOffsetMode.Manual;

    [Tooltip("Local XY shift per unit of virtual Z depth. Used in Manual mode.")]
    [SerializeField] private Vector2 m_ManualLayerOffsetPerDepth;

    [Tooltip("Optional scene light or transform used by Source Forward and Away From Source modes.")]
    [SerializeField] private Transform m_LayerOffsetSource;

    [Tooltip("Reverses the resolved XY slope without changing the source transform.")]
    [SerializeField] private bool m_InvertLayerOffset;

    [Tooltip("Limits near-parallel tracked directions so they cannot create an extreme virtual offset.")]
    [Min(0.01f)] [SerializeField] private float m_MaximumTrackedSlope = 10f;

    private SpriteRenderer _spriteRenderer;
    private VirtualDepthSpriteRenderer _virtualDepthSpriteRenderer;
    private MaterialPropertyBlock _propertyBlock;

    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int LayerOffsetPerDepthId = Shader.PropertyToID("_LayerOffsetPerDepth");

    public float Intensity
    {
        get => m_Intensity;
        set
        {
            m_Intensity = Mathf.Max(0f, value);
            Apply();
        }
    }
    
    private void OnEnable()
    {
        Cache();
        Apply();
    }

    private void LateUpdate()
    {
        if (m_LayerOffsetMode != LayerOffsetMode.Manual && m_LayerOffsetSource != null)
            Apply();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        m_Intensity = Mathf.Max(0f, m_Intensity);
        m_MaximumTrackedSlope = Mathf.Max(0.01f, m_MaximumTrackedSlope);

        Cache();

        if (isActiveAndEnabled)
            Apply();
    }
#endif

    private void OnDidApplyAnimationProperties()
    {
        Apply();
    }

    private void Cache()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_virtualDepthSpriteRenderer == null)
            _virtualDepthSpriteRenderer = GetComponent<VirtualDepthSpriteRenderer>();

        if (_propertyBlock == null)
            _propertyBlock = new MaterialPropertyBlock();
    }

    [ContextMenu("Apply")]
    public void Apply()
    {
        Cache();

        if (_spriteRenderer == null)
            return;

        Vector2 layerOffsetPerDepth = ResolveLayerOffsetPerDepth();

        _spriteRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(IntensityId, m_Intensity);
        _propertyBlock.SetVector(LayerOffsetPerDepthId, new Vector4(layerOffsetPerDepth.x, layerOffsetPerDepth.y, 0f, 0f));
        _spriteRenderer.SetPropertyBlock(_propertyBlock);

        // Each mask occupies SpriteRect + layerOffsetPerDepth * layerDepth.
        // Keep those shifted layers inside the renderer's CPU-side culling bounds.
        _virtualDepthSpriteRenderer.SetLayerOffsetPerDepth(layerOffsetPerDepth);
    }

    private Vector2 ResolveLayerOffsetPerDepth()
    {
        Vector2 layerOffsetPerDepth;

        if (m_LayerOffsetMode == LayerOffsetMode.Manual || m_LayerOffsetSource == null)
        {
            layerOffsetPerDepth = m_ManualLayerOffsetPerDepth;
        }
        else
        {
            Vector3 worldDirection = m_LayerOffsetMode == LayerOffsetMode.SourceForward ? m_LayerOffsetSource.forward : transform.position - m_LayerOffsetSource.position;
            layerOffsetPerDepth = ConvertWorldDirectionToLocalOffsetSlope(worldDirection);
        }

        return m_InvertLayerOffset ? -layerOffsetPerDepth : layerOffsetPerDepth;
    }

    private Vector2 ConvertWorldDirectionToLocalOffsetSlope(Vector3 worldDirection)
    {
        if (worldDirection.sqrMagnitude <= Mathf.Epsilon)
            return Vector2.zero;

        Vector3 localDirection = transform.worldToLocalMatrix.MultiplyVector(worldDirection.normalized);
        Vector2 localXY = new Vector2(localDirection.x, localDirection.y);

        Vector2 slope;
        if (Mathf.Abs(localDirection.z) < MinimumTrackedDirectionZ)
            slope = localXY.sqrMagnitude <= Mathf.Epsilon ? Vector2.zero : localXY.normalized * m_MaximumTrackedSlope;
        else
            slope = localXY / localDirection.z;

        return Vector2.ClampMagnitude(slope, m_MaximumTrackedSlope);
    }
}
