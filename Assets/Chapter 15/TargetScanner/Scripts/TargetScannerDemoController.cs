using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class TargetScannerDemoController : MonoBehaviour
{
    private const uint DefaultScannerRenderingLayerMask = 1u << 1;

    [SerializeField] private Transform target;
    [SerializeField] private RenderingLayerMask scannerRenderingLayerMask = DefaultScannerRenderingLayerMask;
    [SerializeField] private bool scannerOnStart = true;

    private RendererLayerState[] rendererStates;

    public bool IsScannerActive { get; private set; }

    private void Awake()
    {
        if (target == null || (uint)scannerRenderingLayerMask == 0)
        {
            Debug.LogError("Target Scanner demo requires a target and a non-zero rendering layer mask.", this);
            enabled = false;
            return;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogError("Target Scanner demo could not find a Renderer below the assigned target.", this);
            enabled = false;
            return;
        }

        rendererStates = new RendererLayerState[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
            rendererStates[i] = new RendererLayerState(renderers[i]);

        SetScannerActive(scannerOnStart);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
            SetScannerActive(!IsScannerActive);
    }

    public void SetScannerActive(bool active)
    {
        IsScannerActive = active;

        if (rendererStates == null)
            return;

        foreach (RendererLayerState state in rendererStates)
        {
            if (state.Renderer != null)
            {
                state.Renderer.renderingLayerMask = active
                    ? state.OriginalRenderingLayerMask | (uint)scannerRenderingLayerMask
                    : state.OriginalRenderingLayerMask;
            }
        }
    }

    private void OnDestroy()
    {
        if (rendererStates != null && IsScannerActive)
            SetScannerActive(false);
    }

    private sealed class RendererLayerState
    {
        public readonly Renderer Renderer;
        public readonly uint OriginalRenderingLayerMask;

        public RendererLayerState(Renderer renderer)
        {
            Renderer = renderer;
            OriginalRenderingLayerMask = renderer.renderingLayerMask;
        }
    }
}
