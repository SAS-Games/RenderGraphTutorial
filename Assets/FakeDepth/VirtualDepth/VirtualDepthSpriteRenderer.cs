using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[ExecuteAlways]
[AddComponentMenu("Rendering/Virtual Depth/Sprite Renderer")]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[MovedFrom(true, null, null, "VirtualDepthSprite")]
public sealed class VirtualDepthSpriteRenderer : MonoBehaviour
{
    private const int MaxLayerCount = 20;

    [SerializeField] private Sprite m_Sprite;
    [SerializeField] private Color m_EffectColor = Color.black;

    [Tooltip("Number of virtual sprite layers evaluated by the shader.")]
    [Range(1, MaxLayerCount)] [SerializeField] private int m_LayerCount = 20;
    [Tooltip("Total local-space Z extrusion.\n" + "For a normal 2D Unity camera placed on negative Z, " + "use a NEGATIVE value to extend toward the camera.")]
    [SerializeField] private float m_TotalDepth = -1.0f;

    [Tooltip("Controls how virtual layers are distributed through the depth.\n" + "X = normalized layer index\n" + "Y = normalized depth.")]
    [SerializeField] private AnimationCurve m_DepthDistribution = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Tooltip("Controls opacity through the virtual depth.\n" + "X = normalized depth\n" + "Y = alpha.")]
    [SerializeField] private AnimationCurve m_OpacityByDepth = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private SpriteRenderer _spriteRenderer;
    private MaterialPropertyBlock _propertyBlock;
    private readonly float[] _layerDepths = new float[MaxLayerCount];
    private readonly float[] _layerOpacities = new float[MaxLayerCount];
    private Vector2 _layerOffsetPerDepth;
    private Vector4 _spriteRect;
#if UNITY_EDITOR
    private Bounds _pendingRendererBounds;
#endif
    private static readonly int SpriteRectId = Shader.PropertyToID("_SpriteRect");
    private static readonly int SpriteUVRectId = Shader.PropertyToID("_SpriteUVRect");
    private static readonly int LayerCountId = Shader.PropertyToID("_VirtualLayerCount");
    private static readonly int LayerDepthsId = Shader.PropertyToID("_VirtualLayerDepths");
    private static readonly int LayerOpacitiesId = Shader.PropertyToID("_VirtualLayerOpacities");
    private static readonly int EffectColorId = Shader.PropertyToID("_EffectColor");

    private void OnEnable()
    {
        Cache();
        Apply();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= ApplyPendingRendererBounds;
#endif

        if (_spriteRenderer != null)
            _spriteRenderer.ResetLocalBounds();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        m_LayerCount = Mathf.Clamp(m_LayerCount, 1, MaxLayerCount);

        Cache();

        if (isActiveAndEnabled)
            Apply();
    }
#endif

    private void Cache()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_propertyBlock == null)
            _propertyBlock = new MaterialPropertyBlock();
    }

    [ContextMenu("Apply")]
    public void Apply()
    {
        Cache();
        
        if (m_Sprite == null)
            m_Sprite = _spriteRenderer.sprite;

        if (m_Sprite == null)
            return;

        if (_spriteRenderer.sprite != m_Sprite)
            _spriteRenderer.sprite = m_Sprite;

        BuildVirtualLayers();
        ApplyShaderProperties();
    }

    private void BuildVirtualLayers()
    {
        for (int layerIndex = 0; layerIndex < MaxLayerCount; ++layerIndex)
        {
            _layerDepths[layerIndex] = 0f;
            _layerOpacities[layerIndex] = 0f;
        }

        for (int layerIndex = 0; layerIndex < m_LayerCount; ++layerIndex)
        {
            float normalizedLayerIndex = m_LayerCount <= 1 ? 0f : layerIndex / (float)(m_LayerCount - 1);

            float normalizedDepth = Mathf.Clamp01(m_DepthDistribution.Evaluate(normalizedLayerIndex));
            float layerDepth = m_TotalDepth * normalizedDepth;
            float layerOpacity = Mathf.Clamp01(m_OpacityByDepth.Evaluate(normalizedDepth));

            _layerDepths[layerIndex] = layerDepth;
            _layerOpacities[layerIndex] = layerOpacity;
        }
    }

    private void ApplyShaderProperties()
    {
        float pixelsPerUnit = m_Sprite.pixelsPerUnit;

        Vector2 pivot = m_Sprite.pivot;
        float width = m_Sprite.rect.width / pixelsPerUnit;
        float height = m_Sprite.rect.height / pixelsPerUnit;

        float minX = -pivot.x / pixelsPerUnit;
        float minY = -pivot.y / pixelsPerUnit;

        _spriteRect = new Vector4(minX, minY, width, height);
        Rect textureRect = m_Sprite.textureRect;

        Texture texture = m_Sprite.texture;

        Vector4 spriteUVRect = new Vector4(
                textureRect.xMin / texture.width,
                textureRect.yMin / texture.height,
                textureRect.xMax / texture.width,
                textureRect.yMax / texture.height);

        _spriteRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetVector(SpriteRectId, _spriteRect);
        _propertyBlock.SetVector(SpriteUVRectId, spriteUVRect);
        _propertyBlock.SetFloat(LayerCountId, m_LayerCount);
        _propertyBlock.SetFloatArray(LayerDepthsId, _layerDepths);
        _propertyBlock.SetFloatArray(LayerOpacitiesId, _layerOpacities);
        _propertyBlock.SetColor(EffectColorId, m_EffectColor);
        _spriteRenderer.SetPropertyBlock(_propertyBlock);

        ApplyRendererBounds();
    }

    internal void SetLayerOffsetPerDepth(Vector2 layerOffsetPerDepth)
    {
        _layerOffsetPerDepth = layerOffsetPerDepth;

        if (_spriteRenderer != null && _spriteRect.z > 0f && _spriteRect.w > 0f)
            ApplyRendererBounds();
    }

    private void ApplyRendererBounds()
    {
        float minimumDepth = 0f;
        float maximumDepth = 0f;
        float minimumX = _spriteRect.x;
        float maximumX = _spriteRect.x + _spriteRect.z;
        float minimumY = _spriteRect.y;
        float maximumY = _spriteRect.y + _spriteRect.w;

        for (int layerIndex = 0; layerIndex < m_LayerCount; ++layerIndex)
        {
            if (_layerOpacities[layerIndex] <= 0.0001f)
                continue;

            float layerDepth = _layerDepths[layerIndex];
            Vector2 layerOffset = _layerOffsetPerDepth * layerDepth;

            minimumDepth = Mathf.Min(minimumDepth, layerDepth);
            maximumDepth = Mathf.Max(maximumDepth, layerDepth);
            minimumX = Mathf.Min(minimumX, _spriteRect.x + layerOffset.x);
            maximumX = Mathf.Max(maximumX, _spriteRect.x + _spriteRect.z + layerOffset.x);
            minimumY = Mathf.Min(minimumY, _spriteRect.y + layerOffset.y);
            maximumY = Mathf.Max(maximumY, _spriteRect.y + _spriteRect.w + layerOffset.y);
        }

        Vector3 boundsCenter = new Vector3(
                (minimumX + maximumX) * 0.5f,
                (minimumY + maximumY) * 0.5f,
                (minimumDepth + maximumDepth) * 0.5f);

        Vector3 boundsSize = new Vector3(
                maximumX - minimumX,
                maximumY - minimumY,
                Mathf.Max(maximumDepth - minimumDepth, 0.001f));

        SetRendererBounds(new Bounds(boundsCenter, boundsSize));
    }

    private void SetRendererBounds(Bounds bounds)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // Renderer.localBounds sends an internal notification that Unity forbids during
            // OnValidate. Apply it safely on the next editor update instead.
            _pendingRendererBounds = bounds;
            UnityEditor.EditorApplication.delayCall -= ApplyPendingRendererBounds;
            UnityEditor.EditorApplication.delayCall += ApplyPendingRendererBounds;
            return;
        }
#endif

        _spriteRenderer.localBounds = bounds;
    }

#if UNITY_EDITOR
    private void ApplyPendingRendererBounds()
    {
        if (this == null || !isActiveAndEnabled || _spriteRenderer == null)
            return;

        _spriteRenderer.localBounds = _pendingRendererBounds;
    }
#endif
}
