using UnityEngine;

[ExecuteAlways, DisallowMultipleComponent]
public sealed class RenderingLayerGroup : MonoBehaviour
{
    private const int DefaultGameObjectLayer = 0;

    [SerializeField] private RenderingLayerMask renderingLayerMask = 1u << 1;
    [SerializeField] private bool resetGameObjectLayers = true;

    private void OnEnable()
    {
        Apply();
    }

    public void Apply()
    {
        uint mask = (uint)renderingLayerMask;
        if (mask == 0)
        {
            Debug.LogError("Rendering Layer Group requires a non-zero rendering layer mask.", this);
            return;
        }

        if (resetGameObjectLayers)
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in transforms)
                child.gameObject.layer = DefaultGameObjectLayer;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer childRenderer in renderers)
            childRenderer.renderingLayerMask |= mask;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            Apply();
    }
#endif
}
