using UnityEngine;

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
