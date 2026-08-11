using System;
using SAS.StateMachineGraph;
using UnityEngine;

[DisallowMultipleComponent]
public class CombatActionGraphController : MonoBehaviour
{
    [Serializable]
    private sealed class CombatActionDefinition
    {
        public CombatActionId action;
        public ActionGraphAsset graph;
        public bool acceptsCombo;
        public CombatPhase startPhase;
        public CombatPhase releasePhase;
    }

    [Header("Action Graphs")]
    [SerializeField] private CombatActionDefinition[] actions;

    [Header("Runtime Services")]
    [SerializeField] private Actor actor;
    [SerializeField] private CombatActionGraphSignals signals;
    [SerializeField] private CombatStateController combatState;

    private ActionGraphExecutor executor;
    private ActionContext context;
    private int executionVersion;

    public bool IsBusy { get; private set; }
    public CombatActionId CurrentAction { get; private set; }

    private void Awake()
    {
        if (actor == null)
            actor = GetComponentInParent<Actor>();

        if (signals == null)
            signals = GetComponent<CombatActionGraphSignals>();

        if (combatState == null)
            combatState = GetComponent<CombatStateController>();

        executor = new ActionGraphExecutor();
        GameObject actionOwner = actor != null ? actor.gameObject : transform.root.gameObject;
        context = new ActionContext
        {
            Owner = actionOwner,
            Blackboard = actor != null ? actor.Blackboard : null
        };
    }

    private void OnDisable()
    {
        CancelCurrentAction();
    }

    private void OnDestroy()
    {
        executor?.Dispose();
    }

    public bool SubmitSwordInput()
    {
        return Submit(CombatComboInput.Sword, CombatActionId.SwordCombo);
    }

    public bool SubmitShieldInput()
    {
        return Submit(CombatComboInput.Shield, CombatActionId.ShieldAttack);
    }

    private bool Submit(CombatComboInput comboInput, CombatActionId action)
    {
        return IsBusy ? signals != null && signals.TryQueue(comboInput) : TryStart(action);
    }

    public bool TryStartHoldAction(CombatActionId action)
    {
        if (action != CombatActionId.HeavyAttack && action != CombatActionId.ShieldRush)
            return false;

        return TryStart(action);
    }

    public void ReleaseHold(CombatActionId action)
    {
        if (!IsBusy || CurrentAction != action || !TryGetDefinition(action, out CombatActionDefinition definition))
            return;

        SetPhase(definition.releasePhase);
        signals?.SignalHoldReleased();
    }

    private bool TryStart(CombatActionId action)
    {
        return TryGetDefinition(action, out CombatActionDefinition definition) && TryExecute(definition);
    }

    private bool TryGetDefinition(CombatActionId action, out CombatActionDefinition definition)
    {
        if (actions != null)
        {
            // Match the previous dictionary behavior: the last duplicate wins.
            for (int i = actions.Length - 1; i >= 0; i--)
            {
                if (actions[i] != null && actions[i].action == action)
                {
                    definition = actions[i];
                    return true;
                }
            }
        }

        definition = null;
        return false;
    }

    private void CancelCurrentAction()
    {
        if (!IsBusy && (executor == null || !executor.IsExecuting))
            return;

        executionVersion++;
        executor?.CancelExecution();
        CompleteAction();
    }

    private bool TryExecute(CombatActionDefinition definition)
    {
        if (IsBusy || definition.graph == null || executor == null)
            return false;

        if (!executor.Build(definition.graph, context))
            return false;

        IsBusy = true;
        CurrentAction = definition.action;
        signals?.BeginAction(definition.acceptsCombo);
        SetPhase(definition.startPhase);

        int version = ++executionVersion;
        ExecuteAsync(version);
        return true;
    }

    private void SetPhase(CombatPhase phase)
    {
        if (phase != CombatPhase.None)
            combatState?.SetPhase(phase);
    }

    private async void ExecuteAsync(int version)
    {
        try
        {
            await executor.ExecuteAsync(context);
        }
        catch (OperationCanceledException)
        {
            // Expected when the component is disabled or an action is canceled.
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
        finally
        {
            if (version == executionVersion)
                CompleteAction();
        }
    }

    private void CompleteAction()
    {
        signals?.EndAction();
        combatState?.ExitCombat();
        IsBusy = false;
        CurrentAction = CombatActionId.None;
    }
}
