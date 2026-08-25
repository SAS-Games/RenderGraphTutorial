using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(VirtualDepthSprite))]
public sealed class VirtualDepthGodRay : MonoBehaviour
{
    public enum LightDirectionMode
    {
        Manual,
        SourceForward,
        AwayFromSource
    }

    private const float MinimumDirectionZ = 0.0001f;

    [Tooltip("Per-renderer multiplier for the accumulated God Ray light.")]
    [Min(0f)] [SerializeField] private float m_Intensity = 1f;

    [Tooltip("How the local-space light slope is calculated.")]
    [SerializeField] private LightDirectionMode m_DirectionMode = LightDirectionMode.Manual;

    [Tooltip("Local XY shift per unit of virtual Z depth. Used in Manual mode.")]
    [SerializeField] private Vector2 m_ManualLightDirection;

    [Tooltip("Optional scene light or transform used by Source Forward and Away From Source modes.")]
    [SerializeField] private Transform m_DirectionSource;

    [Tooltip("Reverses the resolved XY slope without changing the source transform.")]
    [SerializeField] private bool m_InvertDirection;

    [Tooltip("Limits near-parallel tracked directions so they cannot create an extreme virtual offset.")]
    [Min(0.01f)] [SerializeField] private float m_MaximumTrackedSlope = 10f;

    private SpriteRenderer _spriteRenderer;
    private VirtualDepthSprite _virtualDepthSprite;
    private MaterialPropertyBlock _propertyBlock;

    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int LightDirectionId = Shader.PropertyToID("_LightDirection");

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
        if (m_DirectionMode != LightDirectionMode.Manual && m_DirectionSource != null)
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

        if (_virtualDepthSprite == null)
            _virtualDepthSprite = GetComponent<VirtualDepthSprite>();

        if (_propertyBlock == null)
            _propertyBlock = new MaterialPropertyBlock();
    }

    [ContextMenu("Apply")]
    public void Apply()
    {
        Cache();

        if (_spriteRenderer == null)
            return;

        Vector2 lightDirection = ResolveLightDirection();

        _spriteRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(IntensityId, m_Intensity);
        _propertyBlock.SetVector(LightDirectionId, new Vector4(lightDirection.x, lightDirection.y, 0f, 0f));
        _spriteRenderer.SetPropertyBlock(_propertyBlock);

        // The mask occupies SpriteRect + lightDirection * virtualDepth on every slice.
        // Keep those shifted planes inside the renderer's CPU-side culling bounds.
        _virtualDepthSprite.SetBoundsOffsetPerDepth(lightDirection);
    }

    private Vector2 ResolveLightDirection()
    {
        Vector2 lightDirection;

        if (m_DirectionMode == LightDirectionMode.Manual || m_DirectionSource == null)
        {
            lightDirection = m_ManualLightDirection;
        }
        else
        {
            Vector3 worldDirection = m_DirectionMode == LightDirectionMode.SourceForward ? m_DirectionSource.forward : transform.position - m_DirectionSource.position;
            lightDirection = WorldDirectionToLocalSlope(worldDirection);
        }

        return m_InvertDirection ? -lightDirection : lightDirection;
    }

    private Vector2 WorldDirectionToLocalSlope(Vector3 worldDirection)
    {
        if (worldDirection.sqrMagnitude <= Mathf.Epsilon)
            return Vector2.zero;

        Vector3 localDirection = transform.worldToLocalMatrix.MultiplyVector(worldDirection.normalized);
        Vector2 localXY = new Vector2(localDirection.x, localDirection.y);

        Vector2 slope;
        if (Mathf.Abs(localDirection.z) < MinimumDirectionZ)
            slope = localXY.sqrMagnitude <= Mathf.Epsilon ? Vector2.zero : localXY.normalized * m_MaximumTrackedSlope;
        else
            slope = localXY / localDirection.z;

        return Vector2.ClampMagnitude(slope, m_MaximumTrackedSlope);
    }
}
