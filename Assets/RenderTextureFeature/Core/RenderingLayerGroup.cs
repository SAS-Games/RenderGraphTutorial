using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways, DisallowMultipleComponent]
public sealed class RenderingLayerGroup : MonoBehaviour
{
    private const int DefaultGameObjectLayer = 0;

    [FormerlySerializedAs("renderingLayerMask")] [SerializeField] private RenderingLayerMask m_RenderingLayerMask = 1u << 1;
    [FormerlySerializedAs("resetGameObjectLayers")] [SerializeField] private bool m_ResetGameObjectLayers = true;

    private void OnEnable()
    {
        Apply();
    }

    public void Apply()
    {
        uint mask = (uint)m_RenderingLayerMask;
        if (mask == 0)
        {
            Debug.LogError("Rendering Layer Group requires a non-zero rendering layer mask.", this);
            return;
        }

        if (m_ResetGameObjectLayers)
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in transforms)
                child.gameObject.layer = DefaultGameObjectLayer;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer childRenderer in renderers)
            childRenderer.renderingLayerMask = mask;
    }

    private void OnTransformChildrenChanged()
    {
        if (isActiveAndEnabled)
            Apply();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Apply();
    }
#endif
}