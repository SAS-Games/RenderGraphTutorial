using SAS.StateMachineCharacterController;
using UnityEngine;

[DisallowMultipleComponent]
public class CombatInputBinding : MonoBehaviour
{
    [SerializeField] private InputHandler inputHandler;
    [SerializeField] private CombatActionGraphController combatController;
    [SerializeField] private float heavyHoldThreshold = 0.25f;
    [SerializeField] private float shieldHoldThreshold = 0.25f;
    [SerializeField] private bool enablePrimaryAttack = true;
    [SerializeField] private bool enableSecondaryAttack = true;

    private float primaryDownTime;
    private float secondaryDownTime;
    private bool primaryHeld;
    private bool secondaryHeld;
    private bool heavyActionStarted;
    private bool shieldRushActionStarted;
    private bool primaryConsumedByActiveAction;
    private bool secondaryConsumedByActiveAction;

    private void Awake()
    {
        if (inputHandler == null)
            inputHandler = GetComponentInParent<InputHandler>();

        if (combatController == null)
            combatController = GetComponent<CombatActionGraphController>();
    }

    private void Start()
    {
        if (inputHandler == null || combatController == null)
            return;

        if (enablePrimaryAttack)
        {
            inputHandler.RegisterInputCommand(
                "PrimaryAttack",
                new CombatAttackCommand("PrimaryAttack", OnPrimaryStarted, OnPrimaryCanceled),
                true);
        }

        if (enableSecondaryAttack)
        {
            inputHandler.RegisterInputCommand(
                "SecondaryAttack",
                new CombatAttackCommand("SecondaryAttack", OnSecondaryStarted, OnSecondaryCanceled),
                true);
        }
    }

    private void Update()
    {
        if (primaryHeld && !primaryConsumedByActiveAction &&
            !heavyActionStarted && !combatController.IsBusy &&
            Time.time - primaryDownTime >= heavyHoldThreshold)
        {
            heavyActionStarted = combatController.TryStartHoldAction(CombatActionId.HeavyAttack);
        }

        if (secondaryHeld && !secondaryConsumedByActiveAction &&
            !shieldRushActionStarted && !combatController.IsBusy &&
            Time.time - secondaryDownTime >= shieldHoldThreshold)
        {
            shieldRushActionStarted = combatController.TryStartHoldAction(CombatActionId.ShieldRush);
        }
    }

    private void OnDisable()
    {
        primaryHeld = false;
        secondaryHeld = false;
        heavyActionStarted = false;
        shieldRushActionStarted = false;
        primaryConsumedByActiveAction = false;
        secondaryConsumedByActiveAction = false;
    }

    private void OnPrimaryStarted()
    {
        if (combatController.CurrentAction == CombatActionId.ShieldRush)
            return;

        primaryHeld = true;
        primaryDownTime = Time.time;
        heavyActionStarted = false;
        primaryConsumedByActiveAction = combatController.IsBusy;

        if (primaryConsumedByActiveAction)
            combatController.SubmitSwordInput();
    }

    private void OnPrimaryCanceled()
    {
        if (!primaryHeld)
            return;

        primaryHeld = false;

        if (primaryConsumedByActiveAction)
        {
            primaryConsumedByActiveAction = false;
        }
        else if (heavyActionStarted)
            combatController.ReleaseHold(CombatActionId.HeavyAttack);
        else
            combatController.SubmitSwordInput();

        heavyActionStarted = false;
    }

    private void OnSecondaryStarted()
    {
        if (combatController.CurrentAction == CombatActionId.ShieldRush)
            return;

        secondaryHeld = true;
        secondaryDownTime = Time.time;
        shieldRushActionStarted = false;
        secondaryConsumedByActiveAction = combatController.IsBusy;

        if (secondaryConsumedByActiveAction)
            combatController.SubmitShieldInput();
    }

    private void OnSecondaryCanceled()
    {
        if (!secondaryHeld)
            return;

        secondaryHeld = false;

        if (secondaryConsumedByActiveAction)
        {
            secondaryConsumedByActiveAction = false;
        }
        else if (shieldRushActionStarted)
            combatController.ReleaseHold(CombatActionId.ShieldRush);
        else
            combatController.SubmitShieldInput();

        shieldRushActionStarted = false;
    }
}
