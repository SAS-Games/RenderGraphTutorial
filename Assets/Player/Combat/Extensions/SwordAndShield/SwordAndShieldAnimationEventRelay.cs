using UnityEngine;

/// <summary>
/// Optional adapter for the legacy Sword/Shield animation-event protocol.
/// </summary>
[DisallowMultipleComponent]
public sealed class SwordAndShieldAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private SwordAndShieldCombatSignals signals;

    private void Awake()
    {
        if (signals == null)
            signals = transform.root.GetComponentInChildren<SwordAndShieldCombatSignals>(true);
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

    public void EndQueuedComboStep(int attackIndex)
    {
        signals?.EndQueuedComboStep(attackIndex);
    }

    public void RequestMovement(int movementEvent)
    {
        signals?.RequestMovement(movementEvent);
    }
}
