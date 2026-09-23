using System;
using SAS.Core.TagSystem;
using SAS.StateMachineCharacterController;
using UnityEngine;

public enum CombatInputSubmitPhase
{
    Started,
    Canceled
}

[Serializable]
public sealed class CombatInputActionDefinition
{
    public string inputActionName = "PrimaryAttack";
    public string actionId = "PrimaryAttack";
    public CombatInputSubmitPhase submitPhase = CombatInputSubmitPhase.Started;
}


[DisallowMultipleComponent]
public sealed class CombatInputBinding : MonoBehaviour
{
    [FieldRequiresParent] private InputHandler _inputHandler;
    [FieldRequiresSelf] private CombatActionGraphController _combatController;
    [SerializeField] private CombatInputActionDefinition[] _bindings =
    {
        new CombatInputActionDefinition()
    };

    private void Awake()
    {
        this.Initialize();
    }

    private void Start()
    {
        if (_inputHandler == null || _combatController == null || _bindings == null)
            return;

        foreach (CombatInputActionDefinition binding in _bindings)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.inputActionName) || string.IsNullOrWhiteSpace(binding.actionId))
            {
                continue;
            }

            if (!_combatController.HasAction(binding.actionId))
            {
                Debug.LogError(
                    $"Combat input '{binding.inputActionName}' references unknown action ID '{binding.actionId}'. " +
                    "Add the same action ID to CombatActionGraphController.",
                    this);
                continue;
            }

            CombatInputActionDefinition capturedBinding = binding;
            Action onStarted = capturedBinding.submitPhase == CombatInputSubmitPhase.Started ? () => Submit(capturedBinding) : null;
            Action onCanceled = capturedBinding.submitPhase == CombatInputSubmitPhase.Canceled ? () => Submit(capturedBinding) : null;

            _inputHandler.RegisterInputCommand(capturedBinding.inputActionName, new CombatAttackCommand(capturedBinding.inputActionName, onStarted, onCanceled), true);
        }
    }

    private void Submit(CombatInputActionDefinition binding)
    {
        _combatController.Submit(binding.actionId, binding.inputActionName);
    }
}
