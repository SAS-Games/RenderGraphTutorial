using System;
using SAS.Core.BlackboardSystem;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class CombatActionGraphController : MonoBehaviour
{
    public const string ActionRunningBlackboardKey = "Combat.ActionRunning";
    public const string CurrentActionBlackboardKey = "Combat.CurrentAction";

    [Serializable]
    private sealed class CombatActionDefinition
    {
        public string actionId;
        public ActionGraphAsset graph;
    }

    [FormerlySerializedAs("actions")] [Header("Action Graphs")] [SerializeField] private CombatActionDefinition[] m_Actions;
    [FormerlySerializedAs("actionOwner")] [Header("Runtime Context")] [SerializeField] private GameObject m_ActionOwner;
    [FormerlySerializedAs("actionBlackboard")] [SerializeField] private ActionGraphBlackboardComponent m_ActionBlackboard;
    [FormerlySerializedAs("inputBuffer")] [SerializeField] private ActionGraphInputBuffer m_InputBuffer;

    private ActionGraphExecutor executor;
    private ActionContext context;
    private int executionVersion;

    public bool IsBusy { get; private set; }
    public string CurrentActionId { get; private set; } = string.Empty;
    public Blackboard Blackboard { get; private set; }

    public event Action<string> ActionStarted;
    public event Action<string> ActionCompleted;
    public event Action<string> ActionCancelled;
    public event Action<string, Exception> ActionFailed;
    public event Action<string, string> SignalPublished;

    private void Awake()
    {
        if (m_ActionBlackboard == null)
            m_ActionBlackboard = GetComponent<ActionGraphBlackboardComponent>();

        if (m_InputBuffer == null)
            m_InputBuffer = GetComponent<ActionGraphInputBuffer>();

        if (m_InputBuffer == null)
            m_InputBuffer = gameObject.AddComponent<ActionGraphInputBuffer>();

        Blackboard = m_ActionBlackboard != null ? m_ActionBlackboard.Blackboard : null;
        if (Blackboard == null)
            Debug.LogError("CombatActionGraphController requires an ActionGraphBlackboardComponent.", this);
        else
        {
            Blackboard.SetValue(ActionGraphInputBuffer.BlackboardKey, m_InputBuffer);
            Blackboard.SetValue(ActionRunningBlackboardKey, false);
            Blackboard.SetValue(CurrentActionBlackboardKey, string.Empty);
        }

        GameObject owner = m_ActionOwner != null ? m_ActionOwner : transform.root.gameObject;
        executor = new ActionGraphExecutor();
        context = new ActionContext
        {
            Owner = owner,
            Blackboard = Blackboard
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

    public bool Submit(string actionId, string inputName)
    {
        return IsBusy ? PublishInput(inputName) : TryStart(actionId);
    }

    public bool PublishInput(string inputName)
    {
        return m_InputBuffer != null && m_InputBuffer.Publish(inputName);
    }

    public bool Signal(string signalName)
    {
        return SetBlackboardValue(signalName, true);
    }

    public bool SetBlackboardValue(string key, bool value)
    {
        if (Blackboard == null || string.IsNullOrWhiteSpace(key))
            return false;

        Blackboard.SetValue(key, value);
        SignalPublished?.Invoke(CurrentActionId, key);
        return true;
    }

    public bool SetBlackboardValue(string key, int value)
    {
        if (Blackboard == null || string.IsNullOrWhiteSpace(key))
            return false;

        Blackboard.SetValue(key, value);
        SignalPublished?.Invoke(CurrentActionId, key);
        return true;
    }

    public bool TryStart(string actionId)
    {
        return TryGetDefinition(actionId, out CombatActionDefinition definition) && TryExecute(definition);
    }

    public bool HasAction(string actionId)
    {
        return TryGetDefinition(actionId, out _);
    }

    public void CancelCurrentAction()
    {
        if (!IsBusy && (executor == null || !executor.IsExecuting))
            return;

        executionVersion++;
        executor?.CancelExecution();
        EndAction(ActionEndReason.Cancelled, null);
    }

    private bool TryGetDefinition(string actionId, out CombatActionDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(actionId) && m_Actions != null)
        {
            // The last duplicate wins, matching the previous controller behavior.
            for (int i = m_Actions.Length - 1; i >= 0; i--)
            {
                CombatActionDefinition candidate = m_Actions[i];
                if (candidate != null && string.Equals(candidate.actionId, actionId, StringComparison.Ordinal))
                {
                    definition = candidate;
                    return true;
                }
            }
        }

        definition = null;
        return false;
    }

    private bool TryExecute(CombatActionDefinition definition)
    {
        if (IsBusy || definition?.graph == null || executor == null || Blackboard == null)
            return false;

        if (!executor.Build(definition.graph, context))
            return false;

        m_InputBuffer?.Clear();
        IsBusy = true;
        CurrentActionId = definition.actionId;
        Blackboard.SetValue(ActionRunningBlackboardKey, true);
        Blackboard.SetValue(CurrentActionBlackboardKey, CurrentActionId);

        int version = ++executionVersion;
        ActionStarted?.Invoke(CurrentActionId);
        ExecuteAsync(version);
        return true;
    }

    private async void ExecuteAsync(int version)
    {
        ActionEndReason endReason = ActionEndReason.Completed;
        Exception failure = null;

        try
        {
            await executor.ExecuteAsync(context);
        }
        catch (OperationCanceledException)
        {
            endReason = ActionEndReason.Cancelled;
        }
        catch (Exception exception)
        {
            endReason = ActionEndReason.Failed;
            failure = exception;
            Debug.LogException(exception, this);
        }
        finally
        {
            if (version == executionVersion)
                EndAction(endReason, failure);
        }
    }

    private void EndAction(ActionEndReason reason, Exception failure)
    {
        string endedActionId = CurrentActionId;

        m_InputBuffer?.Clear();
        IsBusy = false;
        CurrentActionId = string.Empty;

        if (Blackboard != null)
        {
            Blackboard.SetValue(ActionRunningBlackboardKey, false);
            Blackboard.SetValue(CurrentActionBlackboardKey, string.Empty);
        }

        if (string.IsNullOrEmpty(endedActionId))
            return;

        switch (reason)
        {
            case ActionEndReason.Completed:
                ActionCompleted?.Invoke(endedActionId);
                break;
            case ActionEndReason.Cancelled:
                ActionCancelled?.Invoke(endedActionId);
                break;
            case ActionEndReason.Failed:
                ActionFailed?.Invoke(endedActionId, failure);
                break;
        }
    }

    private enum ActionEndReason
    {
        Completed,
        Cancelled,
        Failed
    }
}
