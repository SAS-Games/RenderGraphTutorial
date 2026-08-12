using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DamageFreezeEchoInstance : MonoBehaviour
{
    private readonly int EchoProgressId = Shader.PropertyToID("_EchoProgress");
    private readonly int EchoLifetimeProgressId = Shader.PropertyToID("_EchoLifetimeProgress");
    private readonly int EchoSeedId = Shader.PropertyToID("_EchoSeed");
    private readonly int EchoTintId = Shader.PropertyToID("_EchoTint");
    private readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    private readonly int EdgeIntensityId = Shader.PropertyToID("_EdgeIntensity");
    private readonly int DissolveEdgeWidthId = Shader.PropertyToID("_DissolveEdgeWidth");
    private readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
    private readonly int DistortionStrengthId = Shader.PropertyToID("_DistortionStrength");
    private readonly int DistortionFrequencyId = Shader.PropertyToID("_DistortionFrequency");
    private readonly int VerticalDriftId = Shader.PropertyToID("_VerticalDrift");
    private readonly int SurfaceOffsetId = Shader.PropertyToID("_SurfaceOffset");

    private Renderer[] echoRenderers;
    private Mesh[] ownedMeshes;
    private MaterialPropertyBlock propertyBlock;
    private float startTime;
    private float holdDuration;
    private float dissolveDuration;
    private float lifetime;
    private float seed;
    private Color echoTint;
    private Color edgeColor;
    private float edgeIntensity;
    private float dissolveEdgeWidth;
    private float noiseScale;
    private float distortionStrength;
    private float distortionFrequency;
    private float verticalDrift;
    private float surfaceOffset;
    private bool initialized;

    internal void Initialize(List<Renderer> renderers, List<Mesh> generatedMeshes, DamageFreezeEchoEmitter.Settings settings, float echoSeed)
    {
        echoRenderers = renderers.ToArray();
        ownedMeshes = generatedMeshes.ToArray();
        propertyBlock = new MaterialPropertyBlock();
        startTime = Time.unscaledTime;
        holdDuration = Mathf.Max(0f, settings.HoldDuration);
        dissolveDuration = Mathf.Max(0.01f, settings.DissolveDuration);
        lifetime = holdDuration + dissolveDuration;
        seed = echoSeed;
        echoTint = settings.EchoTint;
        edgeColor = settings.EdgeColor;
        edgeIntensity = Mathf.Max(0f, settings.EdgeIntensity);
        dissolveEdgeWidth = Mathf.Max(0.001f, settings.DissolveEdgeWidth);
        noiseScale = Mathf.Max(0.1f, settings.NoiseScale);
        distortionStrength = Mathf.Max(0f, settings.DistortionStrength);
        distortionFrequency = Mathf.Max(0.1f, settings.DistortionFrequency);
        verticalDrift = settings.VerticalDrift;
        surfaceOffset = Mathf.Max(0f, settings.SurfaceOffset);
        initialized = true;

        ApplyProperties(0f, 0f);
    }

    private void Update()
    {
        if (!initialized)
            return;

        float elapsed = Time.unscaledTime - startTime;
        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float lifetimeProgress = Mathf.Clamp01(elapsed / lifetime);
        float dissolveProgress =
            elapsed <= holdDuration ? 0f : Mathf.Clamp01((elapsed - holdDuration) / dissolveDuration);
        ApplyProperties(dissolveProgress, lifetimeProgress);
    }

    public void DisposeNow()
    {
        if (this != null)
            Destroy(gameObject);
    }

    private void ApplyProperties(float dissolveProgress, float lifetimeProgress)
    {
        propertyBlock.Clear();
        propertyBlock.SetFloat(EchoProgressId, dissolveProgress);
        propertyBlock.SetFloat(EchoLifetimeProgressId, lifetimeProgress);
        propertyBlock.SetFloat(EchoSeedId, seed);
        propertyBlock.SetColor(EchoTintId, echoTint);
        propertyBlock.SetColor(EdgeColorId, edgeColor);
        propertyBlock.SetFloat(EdgeIntensityId, edgeIntensity);
        propertyBlock.SetFloat(DissolveEdgeWidthId, dissolveEdgeWidth);
        propertyBlock.SetFloat(NoiseScaleId, noiseScale);
        propertyBlock.SetFloat(DistortionStrengthId, distortionStrength);
        propertyBlock.SetFloat(DistortionFrequencyId, distortionFrequency);
        propertyBlock.SetFloat(VerticalDriftId, verticalDrift);
        propertyBlock.SetFloat(SurfaceOffsetId, surfaceOffset);

        for (int i = 0; i < echoRenderers.Length; i++)
        {
            Renderer echoRenderer = echoRenderers[i];
            if (echoRenderer != null)
                echoRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void OnDestroy()
    {
        if (ownedMeshes == null)
            return;

        for (int i = 0; i < ownedMeshes.Length; i++)
        {
            Mesh mesh = ownedMeshes[i];
            if (mesh != null)
                Destroy(mesh);
        }
    }
}