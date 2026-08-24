using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class FakeDepthSpriteRenderer : MonoBehaviour
{
    private const int MaxSlices = 20;
    [SerializeField] private Sprite m_Sprite;
    [SerializeField] private Color m_Color = Color.black;
    [SerializeField] private bool m_FlipX;
    [SerializeField] private bool m_FlipY;
    
    [Tooltip("Number of sprite copies generated inside one mesh.")] 
    [Range(1, MaxSlices)] [SerializeField] private int m_SliceCount = 20;
    [Tooltip("Total local-space Z extrusion.\n" + "For a normal 2D Unity camera placed on negative Z, " + "use a NEGATIVE value to extend toward the camera.")]
    [SerializeField] private float m_Depth = -1.0f;
    
    [Tooltip("Controls how slices are distributed through the depth.\n" + "X = normalized slice index\n" + "Y = normalized depth.")]
    [SerializeField] private AnimationCurve m_DepthDistribution = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Tooltip("Alpha through the fake depth.\n" + "X = normalized depth.\n" + "0 = base/original sprite.\n" + "1 = furthest extrusion toward camera.")]
    [SerializeField] private AnimationCurve m_AlphaByDepth = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    
    [Header("Sorting")] 
    [SerializeField] private string m_SortingLayerName = "Default";
    [SerializeField] private int _sortingOrder;
    [Header("Material")] [Tooltip("Use the FakeDepthSprite URP material created below.")] 
    [SerializeField] private Material m_Material;
    
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _mesh;
    private MaterialPropertyBlock _propertyBlock;
    
    // Shader property IDs
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void OnEnable()
    {
        CacheComponents();
        Rebuild();
    }

    private void OnDestroy()
    {
        DestroyGeneratedMesh();
    }


#if UNITY_EDITOR
    private void OnValidate()
    {
        m_SliceCount = Mathf.Max(1, m_SliceCount);

        CacheComponents();

        if (!isActiveAndEnabled)
            return;

        Rebuild();
    }
#endif


    private void CacheComponents()
    {
        if (_meshFilter == null)
            _meshFilter = GetComponent<MeshFilter>();

        if (_meshRenderer == null)
            _meshRenderer = GetComponent<MeshRenderer>();

        if (_propertyBlock == null)
            _propertyBlock = new MaterialPropertyBlock();
    }


    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        CacheComponents();

        if (m_Sprite == null)
        {
            ClearMesh();
            return;
        }

        EnsureMesh();

        BuildMesh();
        ApplyRendererSettings();
        ApplyMaterialProperties();
    }


    private void EnsureMesh()
    {
        if (_mesh != null)
            return;

        _mesh = new Mesh
        {
            name = $"{name}_FakeDepthSpriteMesh",
            hideFlags = HideFlags.HideAndDontSave
        };

        _mesh.MarkDynamic();
        _meshFilter.sharedMesh = _mesh;
    }


    private void BuildMesh()
    {
        _mesh.Clear();

        Vector2[] sourceVertices = m_Sprite.vertices;
        Vector2[] sourceUVs = m_Sprite.uv;
        ushort[] sourceTriangles = m_Sprite.triangles;

        if (sourceVertices == null || sourceVertices.Length == 0 || sourceTriangles == null || sourceTriangles.Length == 0)
            return;


        int sourceVertexCount = sourceVertices.Length;
        int totalVertexCount = sourceVertexCount * m_SliceCount;
        int totalIndexCount = sourceTriangles.Length * m_SliceCount;


        // Use UInt32 only if necessary.
        _mesh.indexFormat = totalVertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
        
        var vertices = new List<Vector3>(totalVertexCount);
        var uvs = new List<Vector2>(totalVertexCount);
        var colors = new List<Color32>(totalVertexCount);
        var triangles = new List<int>(totalIndexCount);


        // Vertices
        for (int slice = 0; slice < m_SliceCount; slice++)
        {
            float normalizedSlice = m_SliceCount == 1 ? 0f : slice / (float)(m_SliceCount - 1);
            float depthT = Mathf.Clamp01(m_DepthDistribution.Evaluate(normalizedSlice));
            float localZ = m_Depth * depthT;
            float alpha = Mathf.Clamp01(m_AlphaByDepth.Evaluate(depthT));


            Color layerColor = Color.white;
            layerColor.a = alpha;
            Color32 vertexColor = layerColor;
            
            for (int vertexIndex = 0; vertexIndex < sourceVertexCount; vertexIndex++)
            {
                Vector2 sourceVertex = sourceVertices[vertexIndex];
                float x = m_FlipX ? -sourceVertex.x : sourceVertex.x;
                float y = m_FlipY ? -sourceVertex.y : sourceVertex.y;
                
                vertices.Add(new Vector3(x, y, localZ));
                uvs.Add(sourceUVs[vertexIndex]);
                colors.Add(vertexColor);
            }
        }

        if (m_Depth <= 0f)
        {
            for (int slice = 0; slice < m_SliceCount; slice++)
                AddSliceTriangles(slice, sourceVertexCount, sourceTriangles, triangles);
        }
        else
        {
            for (int slice = m_SliceCount - 1; slice >= 0; slice--)
                AddSliceTriangles(slice, sourceVertexCount, sourceTriangles, triangles);
        }


        _mesh.SetVertices(vertices);
        _mesh.SetUVs(0, uvs);
        _mesh.SetColors(colors);
        _mesh.SetTriangles(triangles, 0, true);


        // Normals aren't needed by our unlit shader.
        _mesh.RecalculateBounds();
        _meshFilter.sharedMesh = _mesh;
    }


    private static void AddSliceTriangles(int slice, int sourceVertexCount, ushort[] sourceTriangles, List<int> destination)
    {
        int vertexOffset = slice * sourceVertexCount;
        for (int i = 0; i < sourceTriangles.Length; i++)
            destination.Add(vertexOffset + sourceTriangles[i]);
    }


    private void ApplyRendererSettings()
    {
        if (_meshRenderer == null)
            return;
        
        if (m_Material != null)
            _meshRenderer.sharedMaterial = m_Material;


        _meshRenderer.sortingLayerName = m_SortingLayerName;
        _meshRenderer.sortingOrder = _sortingOrder;
        _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;
        _meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        _meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        _meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }


    private void ApplyMaterialProperties()
    {
        if (_meshRenderer == null || m_Sprite == null)
            return;


        if (_propertyBlock == null)
            _propertyBlock = new MaterialPropertyBlock();


        _meshRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetTexture(MainTexId, m_Sprite.texture);
        _propertyBlock.SetColor(ColorId, m_Color);
        _meshRenderer.SetPropertyBlock(_propertyBlock);
    }


    private void ClearMesh()
    {
        if (_mesh != null)
            _mesh.Clear();
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
    }
}