using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class VirtualDepthSprite : MonoBehaviour
{
    private const int MaxSlices = 20;

    [SerializeField] private Sprite m_Sprite;
    [SerializeField] private Color m_Color = Color.black;

    [Tooltip("Number of virtual sprite copies evaluated by the shader.")]
    [Range(1, MaxSlices)] [SerializeField] private int m_SliceCount = 20;
    [Tooltip("Total local-space Z extrusion.\n" + "For a normal 2D Unity camera placed on negative Z, " + "use a NEGATIVE value to extend toward the camera.")]
    [SerializeField] private float m_Depth = -1.0f;

    [Tooltip("Controls how virtual slices are distributed through the depth.\n" + "X = normalized slice index\n" + "Y = normalized depth.")]
    [SerializeField] private AnimationCurve m_DepthDistribution = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Tooltip("Controls opacity through the virtual depth.\n" + "X = normalized depth\n" + "Y = alpha.")]
    [SerializeField] private AnimationCurve m_AlphaByDepth = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private SpriteRenderer _spriteRenderer;
    private MaterialPropertyBlock _propertyBlock;
    private readonly float[] _virtualDepths = new float[MaxSlices];
    private readonly float[] _virtualAlphas = new float[MaxSlices];
    private static readonly int SpriteRectId = Shader.PropertyToID("_SpriteRect");
    private static readonly int UVRectId = Shader.PropertyToID("_UVRect");
    private static readonly int SliceCountId = Shader.PropertyToID("_SliceCount");
    private static readonly int VirtualDepthsId = Shader.PropertyToID("_VirtualDepths");
    private static readonly int VirtualAlphasId = Shader.PropertyToID("_VirtualAlphas");
    private static readonly int EffectColorId = Shader.PropertyToID("_EffectColor");

    private void OnEnable()
    {
        Cache();
        Apply();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        m_SliceCount = Mathf.Clamp(m_SliceCount, 1, MaxSlices);

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

        BuildVirtualSlices();
        ApplySpriteData();
    }

    private void BuildVirtualSlices()
    {
        for (int i = 0; i < MaxSlices; ++i)
        {
            _virtualDepths[i] = 0f;
            _virtualAlphas[i] = 0f;
        }

        for (int slice = 0; slice < m_SliceCount; ++slice)
        {
            float sliceT = m_SliceCount <= 1 ? 0f : slice / (float)(m_SliceCount - 1);

            float depthT = Mathf.Clamp01(m_DepthDistribution.Evaluate(sliceT));
            float virtualZ = m_Depth * depthT;
            float alpha = Mathf.Clamp01(m_AlphaByDepth.Evaluate(depthT));

            _virtualDepths[slice] = virtualZ;
            _virtualAlphas[slice] = alpha;
        }
    }

    private void ApplySpriteData()
    {
        float pixelsPerUnit = m_Sprite.pixelsPerUnit;

        Vector2 pivot = m_Sprite.pivot;
        float width = m_Sprite.rect.width / pixelsPerUnit;
        float height = m_Sprite.rect.height / pixelsPerUnit;

        float minX = -pivot.x / pixelsPerUnit;
        float minY = -pivot.y / pixelsPerUnit;

        Vector4 spriteRect = new Vector4(minX, minY, width, height);
        Rect textureRect = m_Sprite.textureRect;

        Texture texture = m_Sprite.texture;

        Vector4 uvRect = new Vector4(
                textureRect.xMin / texture.width,
                textureRect.yMin / texture.height,
                textureRect.xMax / texture.width,
                textureRect.yMax / texture.height);

        _spriteRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetVector(SpriteRectId, spriteRect);
        _propertyBlock.SetVector(UVRectId, uvRect);
        _propertyBlock.SetFloat(SliceCountId, m_SliceCount);
        _propertyBlock.SetFloatArray(VirtualDepthsId, _virtualDepths);
        _propertyBlock.SetFloatArray(VirtualAlphasId, _virtualAlphas);
        _propertyBlock.SetColor(EffectColorId, m_Color);
        _spriteRenderer.SetPropertyBlock(_propertyBlock);
    }
}