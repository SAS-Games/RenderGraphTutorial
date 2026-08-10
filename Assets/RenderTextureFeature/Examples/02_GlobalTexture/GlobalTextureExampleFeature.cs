using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Minimal shader-global consumer for
/// TextureExposureMode.FrameRegistryAndGlobalTexture.
/// </summary>
public sealed class GlobalTextureExampleFeature : ScriptableRendererFeature
{
    public const string RequiredTextureName = "_GlobalTextureExampleTexture";

    private const string ShaderResourcePath =
        "TextureExposureExamples/GlobalTextureExample";

    [Tooltip("Color drawn over non-zero pixels in the global texture.")]
    public Color OverlayColor = new(1.0f, 0.25f, 0.15f, 0.75f);

    [Tooltip("Must run after the ObjectsToRenderTextureFeature output pass.")]
    public RenderPassEvent InjectionPoint = RenderPassEvent.AfterRenderingTransparents;

    private GlobalTextureExamplePass _pass;
    private Material _material;
    private bool _loggedMissingShader;

    public override void Create()
    {
        _pass = new GlobalTextureExamplePass();
        EnsureMaterial();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!EnsureMaterial())
        {
            return;
        }

        _pass ??= new GlobalTextureExamplePass();
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
            Debug.LogError($"{nameof(GlobalTextureExampleFeature)} could not load shader resource '{ShaderResourcePath}'. Keep the sample's Resources folder in the project.");
            _loggedMissingShader = true;
        }

        return false;
    }

    private sealed class GlobalTextureExamplePass : ScriptableRenderPass
    {
        private static readonly int RequiredTextureId = Shader.PropertyToID(RequiredTextureName);
        private static readonly int OverlayColorId = Shader.PropertyToID("_OverlayColor");

        private Material _material;
        private Color _overlayColor;

        private sealed class PassData
        {
            public Material Material;
            public Color OverlayColor;
        }

        public GlobalTextureExamplePass()
        {
            profilingSampler = new ProfilingSampler("Exposure Example - Global Texture");
        }

        public void Setup(RenderPassEvent injectionPoint, Material material, Color overlayColor)
        {
            renderPassEvent = injectionPoint;
            _material = material;
            _overlayColor = overlayColor;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null)
            {
                return;
            }

            var resources = frameData.Get<UniversalResourceData>();
            using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Exposure Example - Global Texture", out PassData passData, profilingSampler);

            passData.Material = _material;
            passData.OverlayColor = _overlayColor;

            // This pass deliberately does not resolve a registry TextureHandle.
            // UseGlobalTexture declares the tracked global binding as its input.
            builder.UseGlobalTexture(RequiredTextureId, AccessFlags.Read);
            builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
            builder.SetRenderFunc((PassData data, RasterGraphContext context) => Execute(data, context));
        }

        private static void Execute(PassData data, RasterGraphContext context)
        {
            data.Material.SetColor(OverlayColorId, data.OverlayColor);
            Blitter.BlitTexture(context.cmd, new Vector4(1, 1, 0, 0), data.Material, 0);
        }
    }
}

