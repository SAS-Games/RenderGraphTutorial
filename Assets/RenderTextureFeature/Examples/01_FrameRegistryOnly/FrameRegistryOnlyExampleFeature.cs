using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Minimal C# Render Graph consumer for TextureExposureMode.FrameRegistryOnly.
/// It resolves the producer's TextureHandle explicitly and draws a blue overlay.
/// </summary>
public sealed class FrameRegistryOnlyExampleFeature : ScriptableRendererFeature
{
    public const string RequiredTextureName = "_FrameRegistryOnlyExampleTexture";
    private const string ShaderResourcePath = "TextureExposureExamples/FrameRegistryOnlyExample";

    [Tooltip("Color drawn over non-zero pixels in the registered texture.")]
    public Color OverlayColor = new(0.1f, 0.65f, 1.0f, 0.75f);

    [Tooltip("Must run after the ObjectsToRenderTextureFeature output pass.")]
    public RenderPassEvent InjectionPoint = RenderPassEvent.AfterRenderingTransparents;

    private FrameRegistryOnlyExamplePass _pass;
    private Material _material;
    private bool _loggedMissingShader;

    public override void Create()
    {
        _pass = new FrameRegistryOnlyExamplePass();
        EnsureMaterial();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!EnsureMaterial())
        {
            return;
        }

        _pass ??= new FrameRegistryOnlyExamplePass();
        _pass.Setup(InjectionPoint, _material, OverlayColor);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_material);
        _material = null;
    }

    private bool EnsureMaterial()
    {
        if (_material != null)
        {
            return true;
        }

        Shader shader = Resources.Load<Shader>(ShaderResourcePath);
        if (shader != null)
        {
            _material = CoreUtils.CreateEngineMaterial(shader);
            return _material != null;
        }

        if (!_loggedMissingShader)
        {
            Debug.LogError($"{nameof(FrameRegistryOnlyExampleFeature)} could not load shader resource '{ShaderResourcePath}'. Keep the sample's Resources folder in the project.");
            _loggedMissingShader = true;
        }

        return false;
    }

    private sealed class FrameRegistryOnlyExamplePass : ScriptableRenderPass
    {
        private static readonly int OverlayColorId = Shader.PropertyToID("_OverlayColor");
        private readonly FrameTextureResolver _resolver = new(nameof(FrameRegistryOnlyExampleFeature));

        private Material _material;
        private Color _overlayColor;

        private sealed class PassData
        {
            public TextureHandle Source;
            public Material Material;
            public Color OverlayColor;
        }

        public FrameRegistryOnlyExamplePass()
        {
            _resolver.SetTextureName(RequiredTextureName);
            profilingSampler = new ProfilingSampler("Exposure Example - Frame Registry Only");
        }

        public void Setup(RenderPassEvent injectionPoint, Material material, Color overlayColor)
        {
            renderPassEvent = injectionPoint;
            _material = material;
            _overlayColor = overlayColor;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null || !_resolver.TryResolve(frameData, out TextureHandle source, out _))
            {
                return;
            }

            var resources = frameData.Get<UniversalResourceData>();
            using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Exposure Example - Frame Registry Only", out PassData passData, profilingSampler);

            passData.Source = source;
            passData.Material = _material;
            passData.OverlayColor = _overlayColor;

            // The registry gives C# the real TextureHandle, so the dependency is explicit.
            builder.UseTexture(source, AccessFlags.Read);
            builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
            builder.SetRenderFunc((PassData data, RasterGraphContext context) => Execute(data, context));
        }

        private static void Execute(PassData data, RasterGraphContext context)
        {
            data.Material.SetColor(OverlayColorId, data.OverlayColor);
            Blitter.BlitTexture(context.cmd, data.Source, new Vector4(1, 1, 0, 0), data.Material, 0);
        }
    }
}

