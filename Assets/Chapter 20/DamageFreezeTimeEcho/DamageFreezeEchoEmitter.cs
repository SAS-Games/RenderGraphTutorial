using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class DamageFreezeEchoEmitter : MonoBehaviour
{
    [FormerlySerializedAs("source")] [SerializeField] private Transform m_Source;
    [FormerlySerializedAs("echoMaterial")] [SerializeField] private Material m_EchoMaterial;
    [FormerlySerializedAs("echoSettings")] [SerializeField] private Settings m_EchoSettings = new();

    [FormerlySerializedAs("captureOnStart")]
    [Header("Demo Triggering")]
    [SerializeField] private bool m_CaptureOnStart = true;
    [FormerlySerializedAs("initialCaptureDelay")] [SerializeField, Min(0f)] private float m_InitialCaptureDelay = 0.2f;
    [FormerlySerializedAs("autoRepeat")] [SerializeField] private bool m_AutoRepeat = true;
    [FormerlySerializedAs("repeatInterval")] [SerializeField, Min(0.1f)] private float m_RepeatInterval = 1.25f;
    [FormerlySerializedAs("maximumActiveEchoes")] [SerializeField, Range(1, 12)] private int m_MaximumActiveEchoes = 5;

    private readonly List<DamageFreezeEchoInstance> _activeEchoes = new();
    private float _nextCaptureTime;
    private int _captureSequence;
    private bool _initialCapturePending;

    [Serializable]
    public sealed class Settings
    {
        [Header("Lifetime")]
        [Min(0f)] public float HoldDuration = 0.18f;
        [Min(0.01f)] public float DissolveDuration = 0.8f;

        [Header("Color")]
        [ColorUsage(true, true)] public Color EchoTint = new(0.06f, 0.55f, 1.6f, 1f);
        [ColorUsage(true, true)] public Color EdgeColor = new(0.4f, 1.4f, 3f, 1f);
        [Range(0f, 8f)] public float EdgeIntensity = 2.5f;

        [Header("Dissolve")]
        [Range(0.001f, 0.3f)] public float DissolveEdgeWidth = 0.08f;
        [Range(0.1f, 20f)] public float NoiseScale = 4.5f;

        [Header("Motion")]
        [Range(0f, 0.5f)] public float DistortionStrength = 0.08f;
        [Range(0.1f, 20f)] public float DistortionFrequency = 5f;
        [Range(-2f, 2f)] public float VerticalDrift = 0.35f;
        [Range(0f, 0.1f)] public float SurfaceOffset = 0.015f;
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        _initialCapturePending = m_CaptureOnStart;
        _nextCaptureTime = Time.unscaledTime + Mathf.Max(0f, m_InitialCaptureDelay);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
        {
            Capture();
            _initialCapturePending = false;
            _nextCaptureTime = Time.unscaledTime + Mathf.Max(0.1f, m_RepeatInterval);
        }

        if (_initialCapturePending && Time.unscaledTime >= _nextCaptureTime)
        {
            Capture();
            _initialCapturePending = false;
            _nextCaptureTime = Time.unscaledTime + Mathf.Max(0.1f, m_RepeatInterval);
        }
        else if (!_initialCapturePending && m_AutoRepeat && Time.unscaledTime >= _nextCaptureTime)
        {
            Capture();
            _nextCaptureTime = Time.unscaledTime + Mathf.Max(0.1f, m_RepeatInterval);
        }
    }

    [ContextMenu("Capture Damage Freeze Echo")]
    public DamageFreezeEchoInstance Capture()
    {
        Transform captureSource = m_Source != null ? m_Source : transform;
        if (!Application.isPlaying || m_EchoMaterial == null || captureSource == null)
        {
            if (Application.isPlaying && m_EchoMaterial == null)
                Debug.LogError("Damage Freeze Echo requires an echo material.", this);
            return null;
        }

        PruneDestroyedEchoes();
        while (_activeEchoes.Count >= Mathf.Max(1, m_MaximumActiveEchoes))
        {
            DamageFreezeEchoInstance oldest = _activeEchoes[0];
            _activeEchoes.RemoveAt(0);
            if (oldest != null)
                oldest.DisposeNow();
        }

        GameObject root = new($"Damage Freeze Echo {_captureSequence + 1}");
        List<Renderer> snapshotRenderers = new();
        List<Mesh> generatedMeshes = new();

        CaptureSkinnedMeshes(captureSource, root.transform, snapshotRenderers, generatedMeshes);
        CaptureRigidMeshes(captureSource, root.transform, snapshotRenderers);

        if (snapshotRenderers.Count == 0)
        {
            Destroy(root);
            Debug.LogWarning("Damage Freeze Echo found no visible mesh renderers to capture.", this);
            return null;
        }

        DamageFreezeEchoInstance echo = root.AddComponent<DamageFreezeEchoInstance>();
        float seed = _captureSequence * 0.6180339f + Time.unscaledTime * 0.173f;
        _captureSequence++;
        echo.Initialize(snapshotRenderers, generatedMeshes, m_EchoSettings, seed);
        _activeEchoes.Add(echo);
        return echo;
    }

    private void CaptureSkinnedMeshes(Transform captureSource, Transform echoRoot, List<Renderer> snapshotRenderers, List<Mesh> generatedMeshes)
    {
        SkinnedMeshRenderer[] sources = captureSource.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            SkinnedMeshRenderer sourceRenderer = sources[i];
            if (!CanCapture(sourceRenderer) || sourceRenderer.sharedMesh == null)
                continue;

            Mesh bakedMesh = new()
            {
                name = $"{sourceRenderer.name} Frozen Pose"
            };
            sourceRenderer.BakeMesh(bakedMesh, true);
            bakedMesh.RecalculateBounds();

            MeshRenderer echoRenderer = CreateEchoRenderer(sourceRenderer, bakedMesh, echoRoot);
            snapshotRenderers.Add(echoRenderer);
            generatedMeshes.Add(bakedMesh);
        }
    }

    private void CaptureRigidMeshes(Transform captureSource, Transform echoRoot, List<Renderer> snapshotRenderers)
    {
        MeshRenderer[] sources = captureSource.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            MeshRenderer sourceRenderer = sources[i];
            MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
            if (!CanCapture(sourceRenderer) || sourceFilter == null || sourceFilter.sharedMesh == null)
                continue;

            snapshotRenderers.Add(CreateEchoRenderer(sourceRenderer, sourceFilter.sharedMesh, echoRoot));
        }
    }

    private MeshRenderer CreateEchoRenderer(Renderer sourceRenderer, Mesh mesh, Transform echoRoot)
    {
        GameObject echoObject = new($"{sourceRenderer.name} Echo");
        echoObject.layer = sourceRenderer.gameObject.layer;
        echoObject.transform.SetParent(echoRoot, false);
        echoObject.transform.SetPositionAndRotation(sourceRenderer.transform.position, sourceRenderer.transform.rotation);
        echoObject.transform.localScale = sourceRenderer.transform.lossyScale;

        MeshFilter meshFilter = echoObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer echoRenderer = echoObject.AddComponent<MeshRenderer>();
        int materialCount = Mathf.Max(1, mesh.subMeshCount);
        Material[] materials = new Material[materialCount];
        for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
            materials[materialIndex] = m_EchoMaterial;

        echoRenderer.sharedMaterials = materials;
        echoRenderer.renderingLayerMask = sourceRenderer.renderingLayerMask;
        echoRenderer.shadowCastingMode = ShadowCastingMode.Off;
        echoRenderer.receiveShadows = false;
        echoRenderer.lightProbeUsage = LightProbeUsage.Off;
        echoRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        echoRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        echoRenderer.allowOcclusionWhenDynamic = sourceRenderer.allowOcclusionWhenDynamic;
        return echoRenderer;
    }

    private static bool CanCapture(Renderer renderer)
    {
        return renderer != null &&
               renderer.enabled &&
               !renderer.forceRenderingOff &&
               renderer.gameObject.activeInHierarchy;
    }

    private void PruneDestroyedEchoes()
    {
        for (int i = _activeEchoes.Count - 1; i >= 0; i--)
        {
            if (_activeEchoes[i] == null)
                _activeEchoes.RemoveAt(i);
        }
    }

    private void OnValidate()
    {
        m_EchoSettings ??= new Settings();
        m_InitialCaptureDelay = Mathf.Max(0f, m_InitialCaptureDelay);
        m_RepeatInterval = Mathf.Max(0.1f, m_RepeatInterval);
        m_MaximumActiveEchoes = Mathf.Clamp(m_MaximumActiveEchoes, 1, 12);
    }
}
