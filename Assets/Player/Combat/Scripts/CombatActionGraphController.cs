using System;
using System.Collections.Generic;
using SAS.StateMachineGraph;
using UnityEngine;

[DisallowMultipleComponent]
public class CombatActionGraphController : MonoBehaviour
{
    [Header("Action Graphs")]
    [SerializeField] private ActionGraphAsset swordComboGraph;
    [SerializeField] private ActionGraphAsset shieldAttackGraph;
    [SerializeField] private ActionGraphAsset heavyAttackGraph;
    [SerializeField] private ActionGraphAsset shieldRushGraph;

    [Header("Runtime Services")]
    [SerializeField] private Actor actor;
    [SerializeField] private CombatActionGraphSignals signals;
    [SerializeField] private CombatStateController combatState;

    private readonly Dictionary<CombatActionId, ActionGraphAsset> runtimeGraphs = new();
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

        foreach (ActionGraphAsset graph in runtimeGraphs.Values)
        {
            if (graph != null)
                Destroy(graph);
        }
    }

    public bool SubmitSwordInput()
    {
        return IsBusy ? signals != null && signals.TryQueue(CombatComboInput.Sword) : TryStart(CombatActionId.SwordCombo);
    }

    public bool SubmitShieldInput()
    {
        return IsBusy ? signals != null && signals.TryQueue(CombatComboInput.Shield) : TryStart(CombatActionId.ShieldAttack);
    }

    public bool TryStartHoldAction(CombatActionId action)
    {
        if (action != CombatActionId.HeavyAttack && action != CombatActionId.ShieldRush)
            return false;

        return TryStart(action);
    }

    public void ReleaseHold(CombatActionId action)
    {
        if (!IsBusy || CurrentAction != action)
            return;

        // Heavy hold preserves the reference controller's movement behavior:
        // locomotion locks only when the button is released into the attack.
        if (action == CombatActionId.HeavyAttack)
            combatState?.SetPhase(CombatPhase.CombatAttack);
        else if (action == CombatActionId.ShieldRush)
            combatState?.SetPhase(CombatPhase.Rush);

        signals?.SignalHoldReleased();
    }

    private bool TryStart(CombatActionId action)
    {
        ActionGraphAsset graph = ResolveGraph(action);
        return TryExecute(graph, action, action == CombatActionId.SwordCombo);
    }

    private void CancelCurrentAction()
    {
        if (!IsBusy && (executor == null || !executor.IsExecuting))
            return;

        executionVersion++;
        executor?.CancelExecution();
        CompleteAction();
    }

    private bool TryExecute(ActionGraphAsset graph, CombatActionId action, bool acceptsCombo)
    {
        if (IsBusy || graph == null || executor == null)
            return false;

        if (!executor.Build(graph, context))
            return false;

        IsBusy = true;
        CurrentAction = action;
        signals?.BeginAction(acceptsCombo);

        if (action == CombatActionId.ShieldRush)
            combatState?.SetPhase(CombatPhase.ShieldHold);
        else if (action != CombatActionId.HeavyAttack)
            combatState?.SetPhase(CombatPhase.CombatAttack);

        int version = ++executionVersion;
        ExecuteAsync(version);
        return true;
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

    private ActionGraphAsset ResolveGraph(CombatActionId action)
    {
        ActionGraphAsset configured = action switch
        {
            CombatActionId.SwordCombo => swordComboGraph,
            CombatActionId.ShieldAttack => shieldAttackGraph,
            CombatActionId.HeavyAttack => heavyAttackGraph,
            CombatActionId.ShieldRush => shieldRushGraph,
            _ => null
        };

        if (configured != null)
            return configured;

        if (runtimeGraphs.TryGetValue(action, out ActionGraphAsset runtimeGraph))
            return runtimeGraph;

        NodeConfig root = CombatActionGraphDefinition.Build(action);
        if (root == null)
            return null;

        runtimeGraph = ScriptableObject.CreateInstance<ActionGraphAsset>();
        runtimeGraph.name = $"Runtime {action} ActionGraph";
        runtimeGraph.hideFlags = HideFlags.HideAndDontSave;
        runtimeGraph.root = root;
        runtimeGraphs[action] = runtimeGraph;
        return runtimeGraph;
    }
}
