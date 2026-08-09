using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class TemporalTrailDemoController : MonoBehaviour
{
    private const uint DefaultTrailRenderingLayerMask = 1u << 1;

    [SerializeField] private Transform trailSource;
    [SerializeField] private RenderingLayerMask trailRenderingLayerMask = DefaultTrailRenderingLayerMask;
    [SerializeField] private bool trailOnStart = true;

    private RendererState[] rendererStates;

    public bool IsTrailActive { get; private set; }

    private void Awake()
    {
        if (trailSource == null || (uint)trailRenderingLayerMask == 0)
        {
            Debug.LogError(
                "Temporal Trail requires a trail source and a non-zero rendering layer mask.",
                this);
            enabled = false;
            return;
        }

        Renderer[] renderers = trailSource.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogError("Temporal Trail could not find a Renderer below its trail source.", this);
            enabled = false;
            return;
        }

        rendererStates = new RendererState[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            rendererStates[i] = new RendererState(renderers[i]);

        SetTrailActive(trailOnStart);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
            SetTrailActive(!IsTrailActive);
    }

    public void SetTrailActive(bool active)
    {
        IsTrailActive = active;

        if (rendererStates == null)
            return;

        uint effectMask = (uint)trailRenderingLayerMask;
        foreach (RendererState state in rendererStates)
            state.SetParticipating(effectMask, active);
    }

    private void OnDestroy()
    {
        if (rendererStates == null)
            return;

        foreach (RendererState state in rendererStates)
            state.Restore();
    }

    private sealed class RendererState
    {
        private readonly Renderer sourceRenderer;
        private readonly uint originalRenderingLayerMask;

        public RendererState(Renderer renderer)
        {
            sourceRenderer = renderer;
            originalRenderingLayerMask = renderer.renderingLayerMask;
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
            if (sourceRenderer != null)
                sourceRenderer.renderingLayerMask = originalRenderingLayerMask;
        }
    }
}
