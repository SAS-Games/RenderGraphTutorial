using UnityEngine;

/// <summary>
/// Forwards animation events to the active combat ActionGraph blackboard.
/// This relay has no dependency on a particular weapon or combat extension.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatActionGraphAnimationRelay : MonoBehaviour
{
    [SerializeField] private CombatActionGraphController actionController;

    private void Awake()
    {
        if (actionController == null)
            actionController = transform.root.GetComponentInChildren<CombatActionGraphController>(true);

        if (actionController == null)
            Debug.LogError("Combat animation relay could not find a CombatActionGraphController.", this);
    }

    public void Signal(string signalName)
    {
        actionController?.Signal(signalName);
    }

    public void SignalInteger(AnimationEvent animationEvent)
    {
        if (animationEvent == null)
            return;

        actionController?.SetBlackboardValue(animationEvent.stringParameter, animationEvent.intParameter);
    }

    public void EndCombo()
    {
        Signal("AnimationEnded");
    }

    public void RequestMovement(int _)
    {
        Signal("MovementRequested");
    }
}
