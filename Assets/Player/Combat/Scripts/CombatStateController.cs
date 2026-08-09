using SAS.StateMachineCharacterController;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class CombatStateController : MonoBehaviour
{
    [SerializeField] private FSMCharacterController characterController;
    [SerializeField] private AnimationController animationController;
    [FormerlySerializedAs("combatStateBool")]
    [SerializeField] private string combatPhaseParameter = "CombatPhase";
    [SerializeField] private int movementLockPriority = 100;

    private readonly object movementLockSource = new object();
    private IMovementVelocityComposer movementComposer;

    public bool IsActive { get; private set; }
    public CombatPhase CurrentPhase { get; private set; }

    private void Awake()
    {
        if (characterController == null)
            characterController = GetComponentInParent<FSMCharacterController>();

        if (animationController == null)
            animationController = GetComponentInParent<AnimationController>();

        movementComposer = characterController as IMovementVelocityComposer;
    }

    private void OnDisable()
    {
        ExitCombat();
    }

    public void SetPhase(CombatPhase phase)
    {
        if (CurrentPhase == phase)
            return;

        CurrentPhase = phase;
        characterController?.Actor.SetInteger(combatPhaseParameter, (int)phase);

        bool shouldLockMovement = phase != CombatPhase.None;
        if (IsActive == shouldLockMovement)
            return;

        IsActive = shouldLockMovement;
        animationController?.SetCombatMovementLocked(shouldLockMovement);

        if (shouldLockMovement)
        {
            movementComposer?.SetMovementVelocityContribution(
                movementLockSource,
                Vector3.zero,
                MovementVelocityContributionMode.OverrideHorizontal,
                movementLockPriority);
        }
        else
        {
            movementComposer?.ClearMovementVelocityContribution(movementLockSource);
        }
    }

    public void ExitCombat()
    {
        SetPhase(CombatPhase.None);
    }
}
