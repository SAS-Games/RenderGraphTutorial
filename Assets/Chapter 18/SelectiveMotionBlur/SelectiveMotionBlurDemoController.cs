using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class SelectiveMotionBlurDemoController : MonoBehaviour
{
    private const uint DefaultBlurRenderingLayerMask = 1u << 1;

    [SerializeField] private Transform blurSource;
    [SerializeField] private RenderingLayerMask blurRenderingLayerMask = DefaultBlurRenderingLayerMask;
    [SerializeField] private bool blurOnStart = true;

    private RendererState[] rendererStates;

    public bool IsBlurActive { get; private set; }

    private void Awake()
    {
        if (blurSource == null || (uint)blurRenderingLayerMask == 0)
        {
            Debug.LogError(
                "Selective Motion Blur requires a source hierarchy and a non-zero rendering layer mask.",
                this);
            enabled = false;
            return;
        }

        Renderer[] renderers = blurSource.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogError("Selective Motion Blur could not find a Renderer below its source.", this);
            enabled = false;
            return;
        }

        rendererStates = new RendererState[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            rendererStates[i] = new RendererState(renderers[i]);

        SetBlurActive(blurOnStart);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        bool togglePressed =
            (keyboard != null && keyboard.qKey.wasPressedThisFrame) ||
            (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame);

        if (togglePressed)
            SetBlurActive(!IsBlurActive);
    }

    public void SetBlurActive(bool active)
    {
        IsBlurActive = active;

        if (rendererStates == null)
            return;

        uint effectMask = (uint)blurRenderingLayerMask;
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
        private readonly MotionVectorGenerationMode originalMotionVectorMode;

        public RendererState(Renderer renderer)
        {
            sourceRenderer = renderer;
            originalRenderingLayerMask = renderer.renderingLayerMask;
            originalMotionVectorMode = renderer.motionVectorGenerationMode;
        }

        public void SetParticipating(uint effectMask, bool participating)
        {
            if (sourceRenderer == null)
                return;

            sourceRenderer.renderingLayerMask = participating
                ? originalRenderingLayerMask | effectMask
                : originalRenderingLayerMask;
            sourceRenderer.motionVectorGenerationMode = participating
                ? MotionVectorGenerationMode.Object
                : originalMotionVectorMode;
        }

        public void Restore()
        {
            if (sourceRenderer != null)
            {
                sourceRenderer.renderingLayerMask = originalRenderingLayerMask;
                sourceRenderer.motionVectorGenerationMode = originalMotionVectorMode;
            }
        }
    }
}
