using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class MaskOutlineFeature : ScriptableRendererFeature
{
    private const string CompositeShaderName = "Hidden/RenderTextureFeature/MaskOutline/Composite";

    public string ProfilingName = "Mask Outline";
    public Material CompositeMaterial;
    public Settings OutlineSettings = new();

    private MaskOutlinePass _pass;
    private Material _compositeMaterial;
    private Material _sourceCompositeMaterial;
    private bool _loggedMissingShader;

    [Serializable]
    public class Settings
    {
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        public string MaskTextureName = "_MaskOutlineMask";
        public Color OutlineColor = new(1.0f, 0.82f, 0.0f, 1.0f);

        [Range(1.0f, 16.0f)]
        public float OutlineWidth = 3.0f;

        [Range(0.0f, 5.0f)]
        public float OutlineIntensity = 1.0f;

        [Range(0.0f, 1.0f)]
        public float MaskThreshold = 0.5f;

        public bool OutsideOnly = true;
    }

    public override void Create()
    {
        _pass ??= new MaskOutlinePass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        OutlineSettings ??= new Settings();

        if (string.IsNullOrWhiteSpace(OutlineSettings.MaskTextureName))
        {
            Debug.LogWarning($"{nameof(MaskOutlineFeature)} skipped because Mask Texture Name is empty.");
            return;
        }

        if (!EnsureCompositeMaterial())
        {
            return;
        }

        _pass.Setup(ProfilingName, OutlineSettings, _compositeMaterial);
        renderer.EnqueuePass(_pass);
    }

    private bool EnsureCompositeMaterial()
    {
        if (CompositeMaterial != null)
        {
            if (_compositeMaterial == null || _sourceCompositeMaterial != CompositeMaterial)
            {
                CoreUtils.Destroy(_compositeMaterial);
                _compositeMaterial = new Material(CompositeMaterial)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _sourceCompositeMaterial = CompositeMaterial;
            }

            return true;
        }

        if (_compositeMaterial != null && _sourceCompositeMaterial == null)
        {
            return true;
        }

        CoreUtils.Destroy(_compositeMaterial);
        _compositeMaterial = CoreUtils.CreateEngineMaterial(CompositeShaderName);
        _sourceCompositeMaterial = null;
        if (_compositeMaterial != null || _loggedMissingShader)
        {
            return _compositeMaterial != null;
        }

        Debug.LogError($"{nameof(MaskOutlineFeature)} could not find shader '{CompositeShaderName}'.");
        _loggedMissingShader = true;
        return false;
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_compositeMaterial);
        _compositeMaterial = null;
        _sourceCompositeMaterial = null;
    }

    private class MaskOutlinePass : ScriptableRenderPass
    {
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int OutlineIntensityId = Shader.PropertyToID("_OutlineIntensity");
        private static readonly int MaskTexelSizeId = Shader.PropertyToID("_MaskTexelSize");
        private static readonly int MaskThresholdId = Shader.PropertyToID("_MaskThreshold");
        private static readonly int OutsideOnlyId = Shader.PropertyToID("_OutsideOnly");

        private string _profilingName;
        private Settings _settings;
        private Material _material;
        private int _maskTexturePropertyId;
        private string _maskTextureName;
        private bool _loggedMissingMaskData;
        private bool _loggedMissingMaskTexture;

        private class PassData
        {
            public TextureHandle MaskTexture;
            public Vector4 MaskTexelSize;
            public Material Material;
            public Color OutlineColor;
            public float OutlineWidth;
            public float OutlineIntensity;
            public float MaskThreshold;
            public float OutsideOnly;
        }

        public void Setup(string profilingName, Settings settings, Material material)
        {
            _profilingName = profilingName;
            _settings = settings;
            _material = material;

            if (_maskTextureName != settings.MaskTextureName)
            {
                _maskTextureName = settings.MaskTextureName;
                _maskTexturePropertyId = Shader.PropertyToID(_maskTextureName);
                _loggedMissingMaskData = false;
                _loggedMissingMaskTexture = false;
            }

            renderPassEvent = settings.RenderPassEvent;
            profilingSampler = new ProfilingSampler(profilingName);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null || _settings == null)
            {
                return;
            }

            if (!frameData.Contains<RenderTexturePass.CustomTextureData>())
            {
                LogMissingMaskDataOnce();
                return;
            }

            RenderTexturePass.CustomTextureData textureData = frameData.Get<RenderTexturePass.CustomTextureData>();
            if (!textureData.TryGetTexture(_maskTexturePropertyId, out TextureHandle maskTexture, out Vector4 maskTexelSize))
            {
                LogMissingMaskTextureOnce();
                return;
            }

            var resourceData = frameData.Get<UniversalResourceData>();
            using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
                _profilingName,
                out PassData passData,
                profilingSampler);

            passData.MaskTexture = maskTexture;
            passData.MaskTexelSize = maskTexelSize;
            passData.Material = _material;
            passData.OutlineColor = _settings.OutlineColor;
            passData.OutlineWidth = Mathf.Clamp(_settings.OutlineWidth, 1.0f, 16.0f);
            passData.OutlineIntensity = Mathf.Max(0.0f, _settings.OutlineIntensity);
            passData.MaskThreshold = Mathf.Clamp01(_settings.MaskThreshold);
            passData.OutsideOnly = _settings.OutsideOnly ? 1.0f : 0.0f;

            builder.UseTexture(maskTexture, AccessFlags.Read);
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
            builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
        }

        private void LogMissingMaskDataOnce()
        {
            if (_loggedMissingMaskData)
            {
                return;
            }

            Debug.LogWarning(
                $"{nameof(MaskOutlineFeature)} did not find render texture data. " +
                $"Add {nameof(ObjectsToRenderTextureFeature)} before this feature and render a mask named '{_maskTextureName}'.");
            _loggedMissingMaskData = true;
        }

        private void LogMissingMaskTextureOnce()
        {
            if (_loggedMissingMaskTexture)
            {
                return;
            }

            Debug.LogWarning(
                $"{nameof(MaskOutlineFeature)} did not find mask texture '{_maskTextureName}'. " +
                $"Make sure a {nameof(ObjectsToRenderTextureFeature)} output uses the same Texture Name.");
            _loggedMissingMaskTexture = true;
        }

        private static void ExecutePass(PassData data, RasterGraphContext context)
        {
            data.Material.SetColor(OutlineColorId, data.OutlineColor);
            data.Material.SetFloat(OutlineWidthId, data.OutlineWidth);
            data.Material.SetFloat(OutlineIntensityId, data.OutlineIntensity);
            data.Material.SetVector(MaskTexelSizeId, data.MaskTexelSize);
            data.Material.SetFloat(MaskThresholdId, data.MaskThreshold);
            data.Material.SetFloat(OutsideOnlyId, data.OutsideOnly);

            Blitter.BlitTexture(context.cmd, data.MaskTexture, new Vector4(1, 1, 0, 0), data.Material, 0);
        }
    }
}
