using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("Rendering/Virtual Depth/Tight Mesh Sprite Renderer")]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class VirtualDepthTightMeshSpriteRenderer : VirtualDepthSpriteRenderer
{
    private const int ProxyDirectionCount = 16;
    private const string ProxyObjectName = "[Virtual Depth Tight Raster Proxy]";

    [Tooltip("Material that uses a Virtual Depth Tight Mesh shader.")]
    [SerializeField] private Material m_TightMeshMaterial;

    private readonly Vector4[] _proxyHalfPlanes = new Vector4[ProxyDirectionCount];
    private readonly Color32[] _proxyVertexColors = new Color32[ProxyDirectionCount];
    private GameObject _proxyObject;
    private MeshRenderer _proxyRenderer;
    private Mesh _proxyMesh;
    private bool _overridesSourceRendering;
    private bool _sourceForceRenderingOff;
    private bool _proxyColorInitialized;
    private Color32 _proxyColor;

    private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
    private static readonly int ProxyHalfPlanesId = Shader.PropertyToID("_VirtualDepthTightProxyHalfPlanes");

    private void LateUpdate()
    {
        if (_proxyRenderer != null)
            SyncProxyRenderer();
    }

    protected override void PrepareEffectRenderer()
    {
        if (m_TightMeshMaterial == null)
        {
            ReleaseEffectRenderer();
            Debug.LogWarning($"'{name}' needs a Tight Mesh material to render its full virtual projection.", this);
            return;
        }

        EnsureProxy();
        BuildProxyHalfPlanes();
        OverrideSourceRendering();
        SyncProxyRenderer();
    }

    protected override Renderer GetEffectRenderer()
    {
        return _proxyRenderer != null ? _proxyRenderer : SourceRenderer;
    }

    protected override void ApplyEffectRendererProperties(MaterialPropertyBlock propertyBlock)
    {
        propertyBlock.SetTexture(MainTextureId, SpriteAsset.texture);
        propertyBlock.SetVectorArray(ProxyHalfPlanesId, _proxyHalfPlanes);
    }

    protected override void ReleaseEffectRenderer()
    {
        RestoreSourceRendering();
        DestroyProxy();
    }

    private void EnsureProxy()
    {
        if (_proxyObject != null && _proxyRenderer != null && _proxyMesh != null)
            return;

        _proxyObject = new GameObject(ProxyObjectName)
        {
            hideFlags = HideFlags.HideAndDontSave,
            layer = gameObject.layer
        };
        _proxyObject.transform.SetParent(transform, false);

        MeshFilter meshFilter = _proxyObject.AddComponent<MeshFilter>();
        _proxyRenderer = _proxyObject.AddComponent<MeshRenderer>();
        _proxyMesh = new Mesh
        {
            name = $"{name}_VirtualDepthTightRasterProxy",
            hideFlags = HideFlags.HideAndDontSave
        };

        var vertices = new Vector3[ProxyDirectionCount];
        var uvs = new Vector2[ProxyDirectionCount];
        var triangles = new int[(ProxyDirectionCount - 2) * 3];

        for (int vertexIndex = 0; vertexIndex < ProxyDirectionCount; ++vertexIndex)
            uvs[vertexIndex] = new Vector2(vertexIndex, 0f);

        int destinationIndex = 0;
        for (int triangleIndex = 1; triangleIndex < ProxyDirectionCount - 1; ++triangleIndex)
        {
            triangles[destinationIndex++] = 0;
            triangles[destinationIndex++] = triangleIndex;
            triangles[destinationIndex++] = triangleIndex + 1;
        }

        _proxyMesh.vertices = vertices;
        _proxyMesh.uv = uvs;
        _proxyMesh.triangles = triangles;
        meshFilter.sharedMesh = _proxyMesh;
        _proxyColorInitialized = false;
    }

    private void BuildProxyHalfPlanes()
    {
        Vector2[] sourceVertices = SpriteAsset.vertices;

        for (int directionIndex = 0; directionIndex < ProxyDirectionCount; ++directionIndex)
        {
            float angle = directionIndex * Mathf.PI * 2f / ProxyDirectionCount;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float support = float.NegativeInfinity;

            for (int vertexIndex = 0; vertexIndex < sourceVertices.Length; ++vertexIndex)
                support = Mathf.Max(support, Vector2.Dot(direction, sourceVertices[vertexIndex]));

            _proxyHalfPlanes[directionIndex] = new Vector4(direction.x, direction.y, support, 0f);
        }
    }

    private void OverrideSourceRendering()
    {
        if (!_overridesSourceRendering)
        {
            _sourceForceRenderingOff = SourceRenderer.forceRenderingOff;
            _overridesSourceRendering = true;
        }

        SourceRenderer.forceRenderingOff = true;
    }

    private void RestoreSourceRendering()
    {
        if (!_overridesSourceRendering)
            return;

        SourceRenderer.forceRenderingOff = _sourceForceRenderingOff;
        _overridesSourceRendering = false;
    }

    private void SyncProxyRenderer()
    {
        _proxyObject.layer = gameObject.layer;
        _proxyRenderer.enabled = SourceRenderer.enabled;
        _proxyRenderer.sharedMaterial = m_TightMeshMaterial;
        _proxyRenderer.sortingLayerID = SourceRenderer.sortingLayerID;
        _proxyRenderer.sortingOrder = SourceRenderer.sortingOrder;
        _proxyRenderer.rendererPriority = SourceRenderer.rendererPriority;
        _proxyRenderer.renderingLayerMask = SourceRenderer.renderingLayerMask;
        SourceRenderer.forceRenderingOff = true;

        Color32 rendererColor = SourceRenderer.color;
        if (_proxyColorInitialized && rendererColor.Equals(_proxyColor))
            return;

        _proxyColor = rendererColor;
        _proxyColorInitialized = true;
        for (int vertexIndex = 0; vertexIndex < _proxyVertexColors.Length; ++vertexIndex)
            _proxyVertexColors[vertexIndex] = rendererColor;

        _proxyMesh.colors32 = _proxyVertexColors;
    }

    private void DestroyProxy()
    {
        if (_proxyRenderer != null)
            _proxyRenderer.enabled = false;

        DestroyRuntimeObject(_proxyMesh);
        DestroyRuntimeObject(_proxyObject);
        _proxyObject = null;
        _proxyRenderer = null;
        _proxyMesh = null;
        _proxyColorInitialized = false;
    }

    private static void DestroyRuntimeObject(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
