using SAS.Core.TagSystem;
using SAS.StateMachineCharacterController;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class SwordAndShieldCombatInputBinding : MonoBehaviour
{
    [FieldRequiresParent] private InputHandler _inputHandler;
    [FieldRequiresSelf] private CombatActionGraphController _combatController;
    [SerializeField] private SwordAndShieldCombatSignals _legacySignals;
    [FormerlySerializedAs("heavyHoldThreshold")] [SerializeField] private float _heavyHoldThreshold = 0.25f;
    [FormerlySerializedAs("shieldHoldThreshold")] [SerializeField] private float _shieldHoldThreshold = 0.25f;
    [FormerlySerializedAs("enablePrimaryAttack")] [SerializeField] private bool _enablePrimaryAttack = true;
    [FormerlySerializedAs("enableSecondaryAttack")] [SerializeField] private bool _enableSecondaryAttack = true;

    private float _primaryDownTime;
    private float _secondaryDownTime;
    private bool _primaryHeld;
    private bool _secondaryHeld;
    private bool _heavyActionStarted;
    private bool _shieldRushActionStarted;
    private bool _primaryConsumedByActiveAction;
    private bool _secondaryConsumedByActiveAction;

    private void Awake()
    {
        this.Initialize();

        if (_legacySignals == null)
            _legacySignals = GetComponent<SwordAndShieldCombatSignals>();
    }

    private void Start()
    {
        if (_inputHandler == null || _combatController == null)
            return;

        if (_enablePrimaryAttack)
            _inputHandler.RegisterInputCommand("PrimaryAttack", new CombatAttackCommand("PrimaryAttack", OnPrimaryStarted, OnPrimaryCanceled), true);

        if (_enableSecondaryAttack)
            _inputHandler.RegisterInputCommand("SecondaryAttack", new CombatAttackCommand("SecondaryAttack", OnSecondaryStarted, OnSecondaryCanceled), true);
    }

    private void Update()
    {
        if (_primaryHeld && !_primaryConsumedByActiveAction && !_heavyActionStarted && !_combatController.IsBusy && Time.time - _primaryDownTime >= _heavyHoldThreshold)
            _heavyActionStarted = _combatController.TryStart(SwordAndShieldActionIds.HeavyAttack);

        if (_secondaryHeld && !_secondaryConsumedByActiveAction && !_shieldRushActionStarted && !_combatController.IsBusy && Time.time - _secondaryDownTime >= _shieldHoldThreshold)
            _shieldRushActionStarted = _combatController.TryStart(SwordAndShieldActionIds.ShieldRush);
    }

    private void OnDisable()
    {
        _primaryHeld = false;
        _secondaryHeld = false;
        _heavyActionStarted = false;
        _shieldRushActionStarted = false;
        _primaryConsumedByActiveAction = false;
        _secondaryConsumedByActiveAction = false;
    }

    private void OnPrimaryStarted()
    {
        if (_combatController.CurrentActionId == SwordAndShieldActionIds.ShieldRush)
            return;

        _primaryHeld = true;
        _primaryDownTime = Time.time;
        _heavyActionStarted = false;
        _primaryConsumedByActiveAction = _combatController.IsBusy;

        if (_primaryConsumedByActiveAction)
            SubmitBufferedInput("PrimaryAttack", CombatComboInput.Sword);
    }

    private void OnPrimaryCanceled()
    {
        if (!_primaryHeld)
            return;

        _primaryHeld = false;

        if (_primaryConsumedByActiveAction)
            _primaryConsumedByActiveAction = false;
        else if (_heavyActionStarted)
            _combatController.Signal(CombatGraphKeys.HoldReleased);
        else
            SubmitAction(SwordAndShieldActionIds.SwordCombo, "PrimaryAttack", CombatComboInput.Sword);

        _heavyActionStarted = false;
    }

    private void OnSecondaryStarted()
    {
        if (_combatController.CurrentActionId == SwordAndShieldActionIds.ShieldRush)
            return;

        _secondaryHeld = true;
        _secondaryDownTime = Time.time;
        _shieldRushActionStarted = false;
        _secondaryConsumedByActiveAction = _combatController.IsBusy;

        if (_secondaryConsumedByActiveAction)
            SubmitBufferedInput("SecondaryAttack", CombatComboInput.Shield);
    }

    private void OnSecondaryCanceled()
    {
        if (!_secondaryHeld)
            return;

        _secondaryHeld = false;

        if (_secondaryConsumedByActiveAction)
            _secondaryConsumedByActiveAction = false;
        else if (_shieldRushActionStarted)
            _combatController.Signal(CombatGraphKeys.HoldReleased);
        else
            SubmitAction(SwordAndShieldActionIds.ShieldAttack, "SecondaryAttack", CombatComboInput.Shield);

        _shieldRushActionStarted = false;
    }

    private void SubmitAction(string actionId, string inputName, CombatComboInput legacyInput)
    {
        if (_combatController.IsBusy)
            SubmitBufferedInput(inputName, legacyInput);
        else
            _combatController.TryStart(actionId);
    }

    private void SubmitBufferedInput(string inputName, CombatComboInput legacyInput)
    {
        _combatController.PublishInput(inputName);
        _legacySignals?.TryQueue(legacyInput);
    }
}
