using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class ThermalVisionDemoController : MonoBehaviour
{
    private const uint DefaultThermalRenderingLayerMask = 1u << 1;
    private static readonly int ThermalHeatId = Shader.PropertyToID("_ThermalHeat");

    [Serializable]
    private struct HeatSource
    {
        public Transform Root;

        [Range(0.05f, 1f)]
        public float Heat;
    }

    [SerializeField] private HeatSource[] heatSources = Array.Empty<HeatSource>();
    [SerializeField] private RenderingLayerMask thermalRenderingLayerMask = DefaultThermalRenderingLayerMask;
    [SerializeField] private bool thermalVisionOnStart = true;
    [SerializeField] private bool throughWallsOnStart;
    [SerializeField, Min(0f)] private float blendDuration = 0.25f;

    private RendererState[] rendererStates = Array.Empty<RendererState>();
    private bool requestedActive;
    private bool useThroughWalls;
    private float currentIntensity;

    public bool IsThermalVisionActive => requestedActive;
    public bool IsThroughWallModeActive => useThroughWalls;

    private void Awake()
    {
        if ((uint)thermalRenderingLayerMask == 0)
        {
            Debug.LogError("Thermal Vision requires a non-zero rendering layer mask.", this);
            enabled = false;
            return;
        }

        rendererStates = CollectRendererStates();
        if (rendererStates.Length == 0)
        {
            Debug.LogError("Thermal Vision could not find any Renderers below its heat-source roots.", this);
            enabled = false;
            return;
        }

        requestedActive = thermalVisionOnStart;
        useThroughWalls = throughWallsOnStart;
        currentIntensity = requestedActive ? 1f : 0f;
    }

    private void OnEnable()
    {
        if (rendererStates.Length == 0)
            return;

        ApplyHeatProperties();
        SetSourcesParticipating(requestedActive || currentIntensity > 0f);
        PublishRuntimeState();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        bool togglePressed =
            (keyboard != null && keyboard.qKey.wasPressedThisFrame) ||
            (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame);

        if (togglePressed)
            SetThermalVisionActive(!requestedActive);

        if (keyboard != null)
        {
            if (keyboard.rKey.wasPressedThisFrame)
                SetThroughWallMode(!useThroughWalls);
        }

        float targetIntensity = requestedActive ? 1f : 0f;
        float speed = blendDuration <= 0.0001f ? float.MaxValue : 1f / blendDuration;
        currentIntensity = Mathf.MoveTowards(currentIntensity, targetIntensity, speed * Time.deltaTime);

        if (!requestedActive && currentIntensity <= 0f)
            SetSourcesParticipating(false);

        PublishRuntimeState();
    }

    public void SetThermalVisionActive(bool active)
    {
        requestedActive = active;
        if (active)
            SetSourcesParticipating(true);

        if (blendDuration <= 0.0001f)
        {
            currentIntensity = active ? 1f : 0f;
            if (!active)
                SetSourcesParticipating(false);
        }

        PublishRuntimeState();
    }

    public void SetThroughWallMode(bool enabled)
    {
        useThroughWalls = enabled;
        PublishRuntimeState();
    }

    private RendererState[] CollectRendererStates()
    {
        var uniqueRenderers = new HashSet<Renderer>();
        var states = new List<RendererState>();

        foreach (HeatSource source in heatSources)
        {
            if (source.Root == null)
                continue;

            float heat = Mathf.Clamp(source.Heat, 0.05f, 1f);
            Renderer[] renderers = source.Root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer sourceRenderer in renderers)
            {
                if (sourceRenderer != null && uniqueRenderers.Add(sourceRenderer))
                    states.Add(new RendererState(sourceRenderer, heat));
            }
        }

        return states.ToArray();
    }

    private void ApplyHeatProperties()
    {
        foreach (RendererState state in rendererStates)
            state.ApplyHeat();
    }

    private void SetSourcesParticipating(bool participating)
    {
        uint effectMask = (uint)thermalRenderingLayerMask;
        foreach (RendererState state in rendererStates)
            state.SetParticipating(effectMask, participating);
    }

    private void PublishRuntimeState()
    {
        ThermalVisionFeature.SetRuntimeState(currentIntensity, useThroughWalls);
    }

    private void OnDisable()
    {
        foreach (RendererState state in rendererStates)
            state.Restore();

        ThermalVisionFeature.SetRuntimeState(0f, false);
    }

    private sealed class RendererState
    {
        private readonly Renderer sourceRenderer;
        private readonly uint originalRenderingLayerMask;
        private readonly float heat;
        private readonly MaterialPropertyBlock originalProperties = new();
        private readonly MaterialPropertyBlock thermalProperties = new();

        public RendererState(Renderer renderer, float sourceHeat)
        {
            sourceRenderer = renderer;
            originalRenderingLayerMask = renderer.renderingLayerMask;
            heat = sourceHeat;
            renderer.GetPropertyBlock(originalProperties);
        }

        public void ApplyHeat()
        {
            if (sourceRenderer == null)
                return;

            sourceRenderer.GetPropertyBlock(thermalProperties);
            thermalProperties.SetFloat(ThermalHeatId, heat);
            sourceRenderer.SetPropertyBlock(thermalProperties);
        }

        public void SetParticipating(uint effectMask, bool participating)
        {
            if (sourceRenderer == null)
                return;

            sourceRenderer.renderingLayerMask = participating
                ? originalRenderingLayerMask | effectMask
                : originalRenderingLayerMask;
        }

        public void Restore()
        {
            if (sourceRenderer == null)
                return;

            sourceRenderer.renderingLayerMask = originalRenderingLayerMask;
            sourceRenderer.SetPropertyBlock(originalProperties);
        }
    }
}
