using System;
using UnityEngine.InputSystem;

public class CombatAttackCommand : ChainedInputCommand
{
    private readonly string inputActionName;

    public CombatAttackCommand(string actionName, Action onStarted, Action onCanceled)
    {
        inputActionName = actionName;
        AddHandler(InputActionPhase.Started, new ConditionalInputHandler(() => true, _ => onStarted?.Invoke()));
        AddHandler(InputActionPhase.Canceled, new ConditionalInputHandler(() => true, _ => onCanceled?.Invoke()));
    }

    protected override string InputActionName => inputActionName;
}