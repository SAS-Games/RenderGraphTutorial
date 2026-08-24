using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

internal sealed class DepthShockwavePass : ScriptableRenderPass
{
    private const int CompositePassIndex = 0;

    private static readonly int SourceTextureId = Shader.PropertyToID("_ShockwaveSourceTexture");
    private static readonly int DepthTextureId = Shader.PropertyToID("_ShockwaveDepthTexture");
    private static readonly int SourceTexelSizeId = Shader.PropertyToID("_SourceTexelSize");
    private static readonly int ShockwaveCountId = Shader.PropertyToID("_ShockwaveCount");
    private static readonly int CentersRadiiId = Shader.PropertyToID("_ShockwaveCentersRadii");
    private static readonly int ParametersId = Shader.PropertyToID("_ShockwaveParameters");
    private static readonly int RingWidthId = Shader.PropertyToID("_RingWidth");
    private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    private static readonly int SecondaryRingOffsetId = Shader.PropertyToID("_SecondaryRingOffset");
    private static readonly int SecondaryRingStrengthId = Shader.PropertyToID("_SecondaryRingStrength");
    private static readonly int DistortionPixelsId = Shader.PropertyToID("_DistortionPixels");
    private static readonly int ChromaticPixelsId = Shader.PropertyToID("_ChromaticPixels");
    private static readonly int WaveColorId = Shader.PropertyToID("_WaveColor");
    private static readonly int EmissionIntensityId = Shader.PropertyToID("_EmissionIntensity");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    private readonly Vector4[] centersAndRadii =
        new Vector4[DepthShockwave.MaximumShaderShockwaves];
    private readonly Vector4[] shockwaveParameters =
        new Vector4[DepthShockwave.MaximumShaderShockwaves];

    private string profilingName;
    private DepthShockwaveFeature.Settings settings;
    private Material material;
    private int sourceTextureId;

    private sealed class PassData
    {
        public TextureHandle Source;
        public TextureHandle Depth;
        public Material Material;
        public Vector4 SourceTexelSize;
        public Vector4[] CentersAndRadii;
        public Vector4[] Parameters;
        public int ShockwaveCount;
        public float RingWidth;
        public float EdgeSoftness;
        public float SecondaryRingOffset;
        public float SecondaryRingStrength;
        public float DistortionPixels;
        public float ChromaticPixels;
        public Color WaveColor;
        public float EmissionIntensity;
        public float Intensity;
    }

    public void Setup(
        string passName,
        DepthShockwaveFeature.Settings passSettings,
        Material passMaterial,
        int snapshotTextureId)
    {
        settings = passSettings;
        material = passMaterial;
        sourceTextureId = snapshotTextureId;

        ConfigureInput(ScriptableRenderPassInput.Depth);
        renderPassEvent = settings.RenderPassEvent;
        profilingSampler = MaskedEffectRenderGraphUtility.GetOrCreateProfilingSampler(
            passName,
            ref profilingName,
            profilingSampler);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (settings == null || material == null)
            return;

        int maximumCount = Mathf.Clamp(
            settings.MaximumSimultaneousShockwaves,
            1,
            DepthShockwave.MaximumShaderShockwaves);
        int shockwaveCount = DepthShockwave.CopyActiveSamples(
            centersAndRadii,
            shockwaveParameters,
            maximumCount);
        if (shockwaveCount == 0)
            return;

        if (!FrameTextureRegistry.TryGet(frameData, out FrameTextureRegistry registry) ||
            !registry.TryGetTexture(sourceTextureId, out TextureHandle source, out Vector4 sourceTexelSize))
        {
            return;
        }

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        TextureHandle depth = resourceData.cameraDepthTexture;
        TextureHandle destination = resourceData.activeColorTexture;
        if (!source.IsValid() || !depth.IsValid() || !destination.IsValid())
            return;

        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
            profilingName,
            out PassData passData,
            profilingSampler);

        passData.Source = source;
        passData.Depth = depth;
        passData.Material = material;
        passData.SourceTexelSize = sourceTexelSize;
        passData.CentersAndRadii = centersAndRadii;
        passData.Parameters = shockwaveParameters;
        passData.ShockwaveCount = shockwaveCount;
        passData.RingWidth = Mathf.Max(0.01f, settings.RingWidth);
        passData.EdgeSoftness = Mathf.Max(0.001f, settings.EdgeSoftness);
        passData.SecondaryRingOffset = Mathf.Max(0f, settings.SecondaryRingOffset);
        passData.SecondaryRingStrength = Mathf.Clamp01(settings.SecondaryRingStrength);
        passData.DistortionPixels = Mathf.Clamp(settings.DistortionPixels, 0f, 40f);
        passData.ChromaticPixels = Mathf.Clamp(settings.ChromaticPixels, 0f, 8f);
        passData.WaveColor = settings.WaveColor;
        passData.EmissionIntensity = Mathf.Clamp(settings.EmissionIntensity, 0f, 5f);
        passData.Intensity = Mathf.Clamp(settings.Intensity, 0f, 2f);

        builder.UseTexture(source, AccessFlags.Read);
        builder.UseTexture(depth, AccessFlags.Read);
        builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
        builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            ExecutePass(data, context));
    }

    private static void ExecutePass(PassData data, RasterGraphContext context)
    {
        data.Material.SetTexture(SourceTextureId, data.Source);
        data.Material.SetTexture(DepthTextureId, data.Depth);
        data.Material.SetVector(SourceTexelSizeId, data.SourceTexelSize);
        data.Material.SetInt(ShockwaveCountId, data.ShockwaveCount);
        data.Material.SetVectorArray(CentersRadiiId, data.CentersAndRadii);
        data.Material.SetVectorArray(ParametersId, data.Parameters);
        data.Material.SetFloat(RingWidthId, data.RingWidth);
        data.Material.SetFloat(EdgeSoftnessId, data.EdgeSoftness);
        data.Material.SetFloat(SecondaryRingOffsetId, data.SecondaryRingOffset);
        data.Material.SetFloat(SecondaryRingStrengthId, data.SecondaryRingStrength);
        data.Material.SetFloat(DistortionPixelsId, data.DistortionPixels);
        data.Material.SetFloat(ChromaticPixelsId, data.ChromaticPixels);
        data.Material.SetColor(WaveColorId, data.WaveColor);
        data.Material.SetFloat(EmissionIntensityId, data.EmissionIntensity);
        data.Material.SetFloat(IntensityId, data.Intensity);

        Blitter.BlitTexture(
            context.cmd,
            data.Depth,
            new Vector4(1f, 1f, 0f, 0f),
            data.Material,
            CompositePassIndex);
    }
}
