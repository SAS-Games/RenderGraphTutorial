using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class DepthShockwaveEmitter : MonoBehaviour
{
    [SerializeField] private Transform center;
    [SerializeField] private Vector3 localOffset = new(0f, 0.25f, 0f);
    [SerializeField, Min(0.01f)] private float duration = 1.35f;
    [SerializeField, Min(0.01f)] private float maxRadius = 12f;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool autoRepeat = true;
    [SerializeField, Min(0.1f)] private float repeatInterval = 2.25f;

    private float nextAutomaticTriggerTime;

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        if (playOnEnable)
            Trigger();

        nextAutomaticTriggerTime = Time.unscaledTime + Mathf.Max(0.1f, repeatInterval);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            Trigger();
            nextAutomaticTriggerTime = Time.unscaledTime + Mathf.Max(0.1f, repeatInterval);
        }

        if (autoRepeat && Time.unscaledTime >= nextAutomaticTriggerTime)
        {
            Trigger();
            nextAutomaticTriggerTime = Time.unscaledTime + Mathf.Max(0.1f, repeatInterval);
        }
    }

    [ContextMenu("Emit Shockwave")]
    public void Trigger()
    {
        DepthShockwave.Emit(GetWorldCenter(), maxRadius, duration);
    }

    public void TriggerAt(Vector3 worldPosition)
    {
        DepthShockwave.Emit(worldPosition, maxRadius, duration);
    }

    private Vector3 GetWorldCenter()
    {
        Transform source = center != null ? center : transform;
        return source.TransformPoint(localOffset);
    }

    private void OnValidate()
    {
        duration = Mathf.Max(0.01f, duration);
        maxRadius = Mathf.Max(0.01f, maxRadius);
        repeatInterval = Mathf.Max(0.1f, repeatInterval);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.65f);
        Gizmos.DrawWireSphere(GetWorldCenter(), maxRadius);
    }
}
