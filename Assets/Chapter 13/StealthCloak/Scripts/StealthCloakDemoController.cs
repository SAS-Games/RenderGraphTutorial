using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class StealthCloakDemoController : MonoBehaviour
{
    private const uint DefaultCloakRenderingLayerMask = 1u << 1;
    [Header("Cloak")]
    [SerializeField] private Material invisibleMaterial;
    [SerializeField] private RenderingLayerMask cloakRenderingLayerMask = DefaultCloakRenderingLayerMask;
    [SerializeField] private bool cloakOnStart = true;
    [SerializeField] private bool cloakToggleEnabled = true;

    private RendererState[] rendererStates;

    public bool IsCloaked { get; private set; }

    private void Awake()
    {
        if ((uint)cloakRenderingLayerMask == 0)
        {
            Debug.LogError("Stealth Cloak requires a non-zero rendering layer mask.", this);
            enabled = false;
            return;
        }

        CacheRendererStates();
        SetCloaked(cloakOnStart);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (cloakToggleEnabled && keyboard != null && keyboard.cKey.wasPressedThisFrame)
            SetCloaked(!IsCloaked);
    }

    private void SetCloaked(bool cloaked)
    {
        if (rendererStates == null || invisibleMaterial == null)
        {
            if (cloaked && invisibleMaterial == null)
                Debug.LogError("Stealth Cloak requires the shared InvisibleMaskProxy material.", this);

            IsCloaked = false;
            return;
        }

        IsCloaked = cloaked;

        foreach (RendererState state in rendererStates)
        {
            Renderer renderer = state.Renderer;
            if (renderer == null)
                continue;

            if (cloaked)
            {
                Material[] invisibleMaterials = new Material[state.Materials.Length];
                for (int i = 0; i < invisibleMaterials.Length; i++)
                    invisibleMaterials[i] = invisibleMaterial;

                renderer.sharedMaterials = invisibleMaterials;
                renderer.renderingLayerMask = state.RenderingLayerMask | (uint)cloakRenderingLayerMask;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }
            else
            {
                renderer.sharedMaterials = state.Materials;
                renderer.renderingLayerMask = state.RenderingLayerMask;
                renderer.shadowCastingMode = state.ShadowCastingMode;
                renderer.receiveShadows = state.ReceiveShadows;
                renderer.motionVectorGenerationMode = state.MotionVectorMode;
            }
        }
    }

    private void OnDestroy()
    {
        if (rendererStates != null && IsCloaked)
            SetCloaked(false);
    }

    private void CacheRendererStates()
    {
        Renderer[] renderers = transform.GetComponentsInChildren<Renderer>(true);
        rendererStates = new RendererState[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
            rendererStates[i] = new RendererState(renderers[i]);
    }
    
    private sealed class RendererState
    {
        public readonly Renderer Renderer;
        public readonly Material[] Materials;
        public readonly uint RenderingLayerMask;
        public readonly ShadowCastingMode ShadowCastingMode;
        public readonly bool ReceiveShadows;
        public readonly MotionVectorGenerationMode MotionVectorMode;

        public RendererState(Renderer renderer)
        {
            Renderer = renderer;
            Materials = renderer.sharedMaterials;
            RenderingLayerMask = renderer.renderingLayerMask;
            ShadowCastingMode = renderer.shadowCastingMode;
            ReceiveShadows = renderer.receiveShadows;
            MotionVectorMode = renderer.motionVectorGenerationMode;
        }
    }
}
