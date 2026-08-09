using SAS.Core.BlackboardSystem;
using SAS.StateMachineGraph;
using UnityEngine;

[DisallowMultipleComponent]
public class CombatActionGraphSignals : MonoBehaviour
{
    [SerializeField] private Actor actor;
    [SerializeField] private CombatStateController combatState;
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
        if (actor == null)
            actor = GetComponentInParent<Actor>();

        if (combatState == null)
            combatState = GetComponent<CombatStateController>();

        blackboard = actor != null ? actor.Blackboard : null;
        ResetBlackboard();
    }

    internal void BeginAction(bool canAcceptCombo)
    {
        actionActive = true;
        acceptsCombo = canAcceptCombo;
        comboWindowOpen = false;
        comboWindowWasOpened = false;
        queuedInput = CombatComboInput.None;
        queuedNextInput = CombatComboInput.None;
        ResetBlackboard();
    }

    internal void EndAction()
    {
        actionActive = false;
        acceptsCombo = false;
        comboWindowOpen = false;
        comboWindowWasOpened = false;
        queuedInput = CombatComboInput.None;
        queuedNextInput = CombatComboInput.None;
    }

    internal bool TryQueue(CombatComboInput input)
    {
        if (!actionActive || !acceptsCombo)
            return false;

        // Keep the first input for this step, and retain one additional input
        // for the following step. Without the second slot, a normal quick
        // three-hit sequence loses its finisher input before Sword02 begins.
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
            DebugLog(comboWindowOpen
                ? $"COMBO OK: {input}"
                : $"COMBO BUFFERED: {input}");
            return true;
        }

        DebugLog("COMBO INPUT IGNORED - TOO LATE");
        return false;
    }

    internal void SignalHoldReleased()
    {
        blackboard.SetValue(CombatGraphKeys.HoldReleased, true);
    }

    // Animation events -------------------------------------------------------

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
        blackboard.SetValue(CombatGraphKeys.ComboInput, resolvedInput.ToString());
        blackboard.SetValue(CombatGraphKeys.ComboDecisionReady, true);
        queuedInput = queuedNextInput;
        queuedNextInput = CombatComboInput.None;
        comboWindowWasOpened = false;
    }

    public void EndCombo()
    {
        // EndCombo is the authoritative animation lifecycle boundary. Also
        // release a missed movement signal so the graph can never retain the
        // combat lock just because a push event was skipped by the Animator.
        blackboard.SetValue(CombatGraphKeys.MovementRequested, true);
        blackboard.SetValue(CombatGraphKeys.AnimationEnded, true);
        combatState?.ExitCombat();
    }

    public void RequestMovement(int _)
    {
        // The reference showcase uses movementEvent to select a configured push.
        // ActionGraphs already own that data, so they only need the timing signal.
        blackboard.SetValue(CombatGraphKeys.MovementRequested, true);
    }

    private void ResetBlackboard()
    {
        if (blackboard == null)
            return;

        blackboard.SetValue(CombatGraphKeys.ComboInput, CombatComboInput.None.ToString());
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
