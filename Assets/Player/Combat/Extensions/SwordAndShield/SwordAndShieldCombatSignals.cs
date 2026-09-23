using System;
using SAS.Core.BlackboardSystem;
using UnityEngine;

[DisallowMultipleComponent]
public class SwordAndShieldCombatSignals : MonoBehaviour
{
    [SerializeField] private CombatActionGraphController actionController;
    [SerializeField] private CombatStateController combatState;
    [SerializeField] private string[] comboActionIds = { SwordAndShieldActionIds.SwordCombo };
    [SerializeField] private bool showDebug;

    private Blackboard blackboard;
    private bool actionActive;
    private bool acceptsCombo;
    private bool comboWindowOpen;
    private bool comboWindowWasOpened;
    private CombatComboInput queuedInput;
    private CombatComboInput queuedNextInput;

    private void Awake()
    {
        if (actionController == null)
            actionController = GetComponent<CombatActionGraphController>();

        if (combatState == null)
            combatState = GetComponent<CombatStateController>();

        ActionGraphBlackboardComponent weaponBlackboard = GetComponent<ActionGraphBlackboardComponent>();
        blackboard = actionController != null && actionController.Blackboard != null
            ? actionController.Blackboard
            : weaponBlackboard != null ? weaponBlackboard.Blackboard : null;
        ResetBlackboard();
    }

    private void OnEnable()
    {
        if (actionController == null)
            actionController = GetComponent<CombatActionGraphController>();

        if (actionController == null)
            return;

        actionController.ActionStarted += HandleActionStarted;
        actionController.ActionCompleted += HandleActionEnded;
        actionController.ActionCancelled += HandleActionEnded;
        actionController.ActionFailed += HandleActionFailed;
    }

    private void OnDisable()
    {
        if (actionController != null)
        {
            actionController.ActionStarted -= HandleActionStarted;
            actionController.ActionCompleted -= HandleActionEnded;
            actionController.ActionCancelled -= HandleActionEnded;
            actionController.ActionFailed -= HandleActionFailed;
        }

        EndAction();
    }

    internal void BindBlackboard(Blackboard sharedBlackboard)
    {
        if (sharedBlackboard == null)
            return;

        blackboard = sharedBlackboard;
        ResetBlackboard();
    }

    internal bool TryQueue(CombatComboInput input)
    {
        if (!actionActive || !acceptsCombo)
            return false;

        if (queuedInput != CombatComboInput.None)
        {
            if (queuedNextInput == CombatComboInput.None)
            {
                queuedNextInput = input;
                DebugLog($"COMBO BUFFERED NEXT: {input}");
            }
            else
            {
                DebugLog($"COMBO INPUT IGNORED - QUEUE FULL: {queuedInput}, {queuedNextInput}");
            }

            return true;
        }

        if (comboWindowOpen || !comboWindowWasOpened)
        {
            queuedInput = input;
            DebugLog(comboWindowOpen ? $"COMBO OK: {input}" : $"COMBO BUFFERED: {input}");
            return true;
        }

        DebugLog("COMBO INPUT IGNORED - TOO LATE");
        return false;
    }

    public void OpenComboWindow()
    {
        if (!actionActive || !acceptsCombo)
            return;

        comboWindowOpen = true;
        comboWindowWasOpened = true;
        DebugLog("WINDOW OPEN");
    }

    public void CloseComboWindow()
    {
        comboWindowOpen = false;
        DebugLog("WINDOW CLOSE");
    }

    public void ResolveComboDecision(int comboStep)
    {
        CloseComboWindow();

        bool allowShieldFinisher = comboStep >= 2;
        bool valid = queuedInput == CombatComboInput.Sword ||
                     (allowShieldFinisher && queuedInput == CombatComboInput.Shield);

        CombatComboInput resolvedInput = valid ? queuedInput : CombatComboInput.None;
        blackboard?.SetValue(CombatGraphKeys.ComboInput, resolvedInput.ToString());
        blackboard?.SetValue(CombatGraphKeys.ComboDecisionReady, true);
        queuedInput = queuedNextInput;
        queuedNextInput = CombatComboInput.None;
        comboWindowWasOpened = false;
    }

    public void EndCombo()
    {
        blackboard?.SetValue(CombatGraphKeys.MovementRequested, true);
        blackboard?.SetValue(CombatGraphKeys.AnimationEnded, true);
        combatState?.ExitCombat();
    }

    public void EndQueuedComboStep(int attackIndex)
    {
        if (blackboard == null ||
            !blackboard.TryGetValue(CombatGraphKeys.QueuedAttackCount, out object rawCount) ||
            rawCount is not int queuedAttackCount ||
            queuedAttackCount <= 0 ||
            attackIndex < queuedAttackCount)
        {
            return;
        }

        EndCombo();
    }

    public void RequestMovement(int _)
    {
        blackboard?.SetValue(CombatGraphKeys.MovementRequested, true);
    }

    private void HandleActionStarted(string actionId)
    {
        if (actionController != null)
            BindBlackboard(actionController.Blackboard);

        BeginAction(IsComboAction(actionId));
    }

    private void HandleActionEnded(string _)
    {
        EndAction();
    }

    private void HandleActionFailed(string _, Exception __)
    {
        EndAction();
    }

    private void BeginAction(bool canAcceptCombo)
    {
        actionActive = true;
        acceptsCombo = canAcceptCombo;
        comboWindowOpen = false;
        comboWindowWasOpened = false;
        queuedInput = CombatComboInput.None;
        queuedNextInput = CombatComboInput.None;
        ResetBlackboard();
    }

    private void EndAction()
    {
        actionActive = false;
        acceptsCombo = false;
        comboWindowOpen = false;
        comboWindowWasOpened = false;
        queuedInput = CombatComboInput.None;
        queuedNextInput = CombatComboInput.None;

        blackboard?.SetValue(CombatGraphKeys.QueuedAttackCount, 0);
    }

    private bool IsComboAction(string actionId)
    {
        if (comboActionIds == null)
            return false;

        foreach (string comboActionId in comboActionIds)
        {
            if (string.Equals(comboActionId, actionId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private void ResetBlackboard()
    {
        if (blackboard == null)
            return;

        blackboard.SetValue(CombatGraphKeys.ComboInput, CombatComboInput.None.ToString());
        blackboard.SetValue(CombatGraphKeys.QueuedAttackCount, 0);
        blackboard.SetValue(CombatGraphKeys.CompletedAttackCount, 0);
        blackboard.SetValue(CombatGraphKeys.ComboStep, 0);
        blackboard.SetValue(CombatGraphKeys.ComboDecisionReady, false);
        blackboard.SetValue(CombatGraphKeys.AnimationEnded, false);
        blackboard.SetValue(CombatGraphKeys.HoldReleased, false);
        blackboard.SetValue(CombatGraphKeys.RushHit, false);
        blackboard.SetValue(CombatGraphKeys.MovementRequested, false);
    }

    private void DebugLog(string message)
    {
        if (showDebug)
            Debug.Log(message, this);
    }
}
