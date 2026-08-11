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

    public CombatPhase CurrentPhase { get; private set; }
    public bool IsActive => CurrentPhase != CombatPhase.None;

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

        bool wasActive = IsActive;
        CurrentPhase = phase;
        characterController?.Actor.SetInteger(combatPhaseParameter, (int)phase);

        if (wasActive != IsActive)
            SetMovementLocked(IsActive);
    }

    private void SetMovementLocked(bool locked)
    {
        animationController?.SetCombatMovementLocked(locked);

        if (locked)
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
