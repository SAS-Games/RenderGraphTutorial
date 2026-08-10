using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Displays the globally published mask produced by FrameTextureProcessingFeature.
/// The producer and processing stages are configured in the example renderer asset.
/// </summary>
public sealed class FrameTextureProcessingExampleFeature : ScriptableRendererFeature
{
    public const string RequiredTextureName = "_FrameProcessingResult";

    private const string ShaderResourcePath =
        "FrameTextureProcessingExample/ProcessedMaskOverlay";

    [Tooltip("Color drawn over pixels that remain after mask processing.")]
    public Color OverlayColor = new(0.8f, 0.2f, 1.0f, 0.85f);

    [Tooltip("Must run after FrameTextureProcessingFeature.")]
    public RenderPassEvent InjectionPoint = RenderPassEvent.AfterRenderingTransparents;

    private ProcessedMaskOverlayPass _pass;
    private Material _material;
    private bool _loggedMissingShader;

    public override void Create()
    {
        _pass = new ProcessedMaskOverlayPass();
        EnsureMaterial();
    }

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData)
    {
        if (!EnsureMaterial())
        {
            return;
        }

        _pass ??= new ProcessedMaskOverlayPass();
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
            Debug.LogError(
                $"{nameof(FrameTextureProcessingExampleFeature)} could not load " +
                $"shader resource '{ShaderResourcePath}'.");
            _loggedMissingShader = true;
        }

        return false;
    }

    private sealed class ProcessedMaskOverlayPass : ScriptableRenderPass
    {
        private static readonly int RequiredTextureId =
            Shader.PropertyToID(RequiredTextureName);
        private static readonly int OverlayColorId = Shader.PropertyToID("_OverlayColor");

        private Material _material;
        private Color _overlayColor;

        private sealed class PassData
        {
            public Material Material;
            public Color OverlayColor;
        }

        public ProcessedMaskOverlayPass()
        {
            profilingSampler = new ProfilingSampler(
                "Frame Texture Processing Example - Result");
        }

        public void Setup(
            RenderPassEvent injectionPoint,
            Material material,
            Color overlayColor)
        {
            renderPassEvent = injectionPoint;
            _material = material;
            _overlayColor = overlayColor;
        }

        public override void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            if (_material == null)
            {
                return;
            }

            var resources = frameData.Get<UniversalResourceData>();
            using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
                "Frame Texture Processing Example - Result",
                out PassData passData,
                profilingSampler);

            passData.Material = _material;
            passData.OverlayColor = _overlayColor;

            // FrameTextureProcessingFeature publishes its output globally.
            // This declaration tells Render Graph which tracked global binding
            // the shader samples and orders this pass after the processor.
            builder.UseGlobalTexture(RequiredTextureId, AccessFlags.Read);
            builder.SetRenderAttachment(
                resources.activeColorTexture,
                0,
                AccessFlags.ReadWrite);
            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                Execute(data, context));
        }

        private static void Execute(PassData data, RasterGraphContext context)
        {
            data.Material.SetColor(OverlayColorId, data.OverlayColor);
            Blitter.BlitTexture(
                context.cmd,
                new Vector4(1, 1, 0, 0),
                data.Material,
                0);
        }
    }
}
