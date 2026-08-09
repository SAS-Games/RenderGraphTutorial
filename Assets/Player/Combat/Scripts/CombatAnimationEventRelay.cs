using UnityEngine;

/// <summary>
/// Animation Events are delivered only to components beside the Animator.
/// The combat receiver lives on the Weapon sibling, so this component forwards
/// clip events without forcing combat orchestration back onto the character root.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private CombatActionGraphSignals signals;

    private void Awake()
    {
        if (signals == null)
            signals = transform.root.GetComponentInChildren<CombatActionGraphSignals>(true);

        if (signals == null)
            Debug.LogError("Combat animation relay could not find CombatActionGraphSignals.", this);
    }

    public void OpenComboWindow()
    {
        signals?.OpenComboWindow();
    }

    public void CloseComboWindow()
    {
        signals?.CloseComboWindow();
    }

    public void ResolveComboDecision(int comboStep)
    {
        signals?.ResolveComboDecision(comboStep);
    }

    public void EndCombo()
    {
        signals?.EndCombo();
    }

    public void RequestMovement(int movementEvent)
    {
        signals?.RequestMovement(movementEvent);
    }
}
