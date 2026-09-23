using System;
using System.Threading;
using SAS.Core.BlackboardSystem;
using UnityEngine;

[Serializable]
public sealed class CollectBufferedInputCountData
{
    [Tooltip("Input name published by the combat controller, such as Sword or Shield.")]
    public string inputName = "PrimaryAttack";

    [Tooltip("Blackboard integer that receives the live accepted-input count.")]
    public string outputKey = "QueuedAttackCount";

    [Tooltip("Blackboard integer reset before animation steps begin and updated by animation events.")]
    public string completionKey = "CompletedAttackCount";

    [Tooltip("Optional Animator integer updated together with the blackboard value.")]
    public string animatorParameterName = "QueuedAttackCount";

    [Min(0f)] public float duration = 0.5f;
    [Min(0)] public int initialCount = 1;
    [Min(1)] public int maximumCount = 3;
    public bool useUnscaledTime;
}

[NodeBinding(typeof(CollectBufferedInputCountActionNode))]
[Serializable]
public sealed class CollectBufferedInputCountProvider : ActionDataProvider<CollectBufferedInputCountData>
{
}

[ActionNodeMenu("Input/Collect Buffered Input Count")]
public sealed class CollectBufferedInputCountActionNode : ActionNode<CollectBufferedInputCountData>
{
    public CollectBufferedInputCountActionNode(ActionDataProvider<CollectBufferedInputCountData> dataProvider) : base(dataProvider)
    {
    }

    public override async Awaitable ExecuteAsync(ActionContext context, CancellationToken token)
    {
        await Awaitable.MainThreadAsync();
        token.ThrowIfCancellationRequested();

        CollectBufferedInputCountData data = _selector.GetNext();
        if (data == null || string.IsNullOrWhiteSpace(data.inputName) || string.IsNullOrWhiteSpace(data.outputKey))
            return;

        var blackboard = ActionGraphBlackboardUtility.RequireBlackboard(context);
        if (!ActionGraphBlackboardUtility.TryGet(context, ActionGraphInputBuffer.BlackboardKey, out ActionGraphInputBuffer inputBuffer) || inputBuffer == null)
            throw new InvalidOperationException("Collect Buffered Input Count requires an ActionGraphInputBuffer registered on the graph blackboard.");

        Animator animator = ResolveAnimator(context);
        int animatorParameterHash = string.IsNullOrWhiteSpace(data.animatorParameterName) ? 0 : Animator.StringToHash(data.animatorParameterName);
        int maximumCount = Mathf.Max(1, data.maximumCount);
        int count = Mathf.Clamp(data.initialCount, 0, maximumCount);
        
        if (!string.IsNullOrWhiteSpace(data.completionKey))
            blackboard.SetValue(data.completionKey, 0);
        ApplyCount(blackboard, animator, animatorParameterHash, data.outputKey, count);

        float deadline = CurrentTime(data.useUnscaledTime) + Mathf.Max(0f, data.duration);
        while (count < maximumCount && CurrentTime(data.useUnscaledTime) <= deadline)
        {
            token.ThrowIfCancellationRequested();

            while (count < maximumCount && inputBuffer.TryConsume(data.inputName))
            {
                count++;
                ApplyCount(blackboard, animator, animatorParameterHash, data.outputKey, count);
            }

            if (count >= maximumCount)
                break;

            await Awaitable.NextFrameAsync(token);
        }
    }

    private static Animator ResolveAnimator(ActionContext context)
    {
        if (context?.Owner == null)
            return null;

        Animator animator = context.Owner.GetComponentInParent<Animator>();
        return animator != null ? animator : context.Owner.GetComponentInChildren<Animator>(true);
    }

    private static float CurrentTime(bool useUnscaledTime)
    {
        return useUnscaledTime ? Time.unscaledTime : Time.time;
    }

    private static void ApplyCount(Blackboard blackboard, Animator animator, int animatorParameterHash, string outputKey, int count)
    {
        blackboard.SetValue(outputKey, count);

        if (animator != null && animatorParameterHash != 0)
            animator.SetInteger(animatorParameterHash, count);
    }
}

[Serializable]
public sealed class BufferedInputCompletionCondition : ICondition
{
    public string completedCountKey = "CompletedAttackCount";
    public string queuedCountKey = "QueuedAttackCount";
    public bool resultWhenMissing;

    public bool Evaluate(ActionContext context)
    {
        if (!ActionGraphBlackboardUtility.TryGet(context, completedCountKey, out int completedCount) || !ActionGraphBlackboardUtility.TryGet(context, queuedCountKey, out int queuedCount))
            return resultWhenMissing;

        return queuedCount > 0 && completedCount >= queuedCount;
    }
}

public abstract class CombatComboActionNode<T> : ActionNode<T>
{
    protected CombatComboActionNode(ActionDataProvider<T> dataProvider) : base(dataProvider)
    {
    }

    protected static CombatActionContext RequireCombatContext(ActionContext context)
    {
        if (context is CombatActionContext combatContext)
            return combatContext;

        throw new InvalidOperationException("Combo node requires CombatActionContext.");
    }
}

[Serializable]
public sealed class CombatComboResetData
{
    public string queuedCountKey = "QueuedAttackCount";
    public string completedCountKey = "CompletedAttackCount";
    public string comboStepKey = "ComboStep";
    public string[] additionalIntegerKeys = Array.Empty<string>();
    public string animatorParameterName = "QueuedAttackCount";
}

[NodeBinding(typeof(CombatComboResetActionNode))]
[Serializable]
public sealed class CombatComboResetProvider : ActionDataProvider<CombatComboResetData>
{
}

[ActionNodeMenu("Combat/Combo Reset", "Returns the combo to its first attack and clears its runtime counters.")]
public sealed class CombatComboResetActionNode : CombatComboActionNode<CombatComboResetData>
{
    public CombatComboResetActionNode(ActionDataProvider<CombatComboResetData> dataProvider) : base(dataProvider)
    {
    }

    public override async Awaitable ExecuteAsync(ActionContext context, CancellationToken token)
    {
        await Awaitable.MainThreadAsync();
        token.ThrowIfCancellationRequested();

        CombatActionContext combatContext = RequireCombatContext(context);
        CombatComboResetData data = _selector.GetNext() ?? new CombatComboResetData();
        Blackboard blackboard = ActionGraphBlackboardUtility.RequireBlackboard(context);

        combatContext.ConfigureComboCleanup(() =>
        {
            SetInteger(blackboard, data.queuedCountKey, 0);
            SetInteger(blackboard, data.completedCountKey, 0);
            SetInteger(blackboard, data.comboStepKey, 0);

            if (data.additionalIntegerKeys != null)
            {
                for (int i = 0; i < data.additionalIntegerKeys.Length; i++)
                    SetInteger(blackboard, data.additionalIntegerKeys[i], 0);
            }

            if (combatContext.Animator != null && !string.IsNullOrWhiteSpace(data.animatorParameterName))
                combatContext.Animator.SetInteger(data.animatorParameterName, 0);
        });
        combatContext.ClearComboRuntime();
    }

    private static void SetInteger(Blackboard blackboard, string key, int value)
    {
        if (!string.IsNullOrWhiteSpace(key))
            blackboard.SetValue(key, value);
    }
}

[Serializable]
public sealed class CombatComboBeginCurrentAttackData
{
    public string queuedCountKey = "QueuedAttackCount";
    public string comboStepKey = "ComboStep";
    public string animatorParameterName = "QueuedAttackCount";
}

[NodeBinding(typeof(CombatComboBeginCurrentAttackActionNode))]
[Serializable]
public sealed class CombatComboBeginCurrentAttackProvider : ActionDataProvider<CombatComboBeginCurrentAttackData>
{
}

[ActionNodeMenu("Combat/Combo Begin Current Attack", "Starts the current combo step and publishes its one-based attack count.")]
public sealed class CombatComboBeginCurrentAttackActionNode : CombatComboActionNode<CombatComboBeginCurrentAttackData>
{
    public CombatComboBeginCurrentAttackActionNode(ActionDataProvider<CombatComboBeginCurrentAttackData> dataProvider) : base(dataProvider)
    {
    }

    public override async Awaitable ExecuteAsync(ActionContext context, CancellationToken token)
    {
        await Awaitable.MainThreadAsync();
        token.ThrowIfCancellationRequested();

        CombatActionContext combatContext = RequireCombatContext(context);
        CombatComboBeginCurrentAttackData data = _selector.GetNext() ?? new CombatComboBeginCurrentAttackData();
        Blackboard blackboard = ActionGraphBlackboardUtility.RequireBlackboard(context);
        int attackCount = combatContext.CurrentAttackIndex + 1;

        combatContext.BeginCurrentAttack();

        if (!string.IsNullOrWhiteSpace(data.comboStepKey))
            blackboard.SetValue(data.comboStepKey, combatContext.CurrentAttackIndex);

        if (!string.IsNullOrWhiteSpace(data.queuedCountKey))
            blackboard.SetValue(data.queuedCountKey, attackCount);

        if (combatContext.Animator != null && !string.IsNullOrWhiteSpace(data.animatorParameterName))
            combatContext.Animator.SetInteger(data.animatorParameterName, attackCount);
    }
}

[Serializable]
public sealed class CombatComboWaitInputData
{
    public string inputName = "PrimaryAttack";
    public string queuedCountKey = "QueuedAttackCount";
    public string animatorParameterName = "QueuedAttackCount";
    [Min(1)] public int comboCount = 3;
    [Min(0f)] public float duration = 0.5f;
    public bool useUnscaledTime;
}

[NodeBinding(typeof(CombatComboWaitInputActionNode))]
[Serializable]
public sealed class CombatComboWaitInputProvider : ActionDataProvider<CombatComboWaitInputData>
{
}

[ActionNodeMenu("Combat/Combo Wait Input", "Consumes one buffered input during the current attack's combo window.")]
public sealed class CombatComboWaitInputActionNode : CombatComboActionNode<CombatComboWaitInputData>
{
    public CombatComboWaitInputActionNode(ActionDataProvider<CombatComboWaitInputData> dataProvider) : base(dataProvider)
    {
    }

    public override async Awaitable ExecuteAsync(ActionContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        CombatActionContext combatContext = RequireCombatContext(context);
        CombatComboWaitInputData data = _selector.GetNext() ?? new CombatComboWaitInputData();
        combatContext.ComboInputAccepted = false;

        int comboCount = Mathf.Max(1, data.comboCount);
        if (combatContext.CurrentAttackIndex >= comboCount - 1 ||
            combatContext.InputBuffer == null ||
            string.IsNullOrWhiteSpace(data.inputName))
        {
            return;
        }

        float deadline = CurrentTime(data.useUnscaledTime) + Mathf.Max(0f, data.duration);
        do
        {
            token.ThrowIfCancellationRequested();

            if (combatContext.InputBuffer.TryConsume(data.inputName))
            {
                combatContext.ComboInputAccepted = true;
                int queuedCount = combatContext.CurrentAttackIndex + 2;

                if (!string.IsNullOrWhiteSpace(data.queuedCountKey))
                    ActionGraphBlackboardUtility.RequireBlackboard(context).SetValue(data.queuedCountKey, queuedCount);

                if (combatContext.Animator != null && !string.IsNullOrWhiteSpace(data.animatorParameterName))
                    combatContext.Animator.SetInteger(data.animatorParameterName, queuedCount);

                return;
            }

            await Awaitable.NextFrameAsync(token);
        }
        while (CurrentTime(data.useUnscaledTime) <= deadline);
    }

    private static float CurrentTime(bool useUnscaledTime)
    {
        return useUnscaledTime ? Time.unscaledTime : Time.time;
    }
}

[Serializable]
public sealed class CombatComboWaitForCompletionData
{
    public string completedCountKey = "CompletedAttackCount";
}

[NodeBinding(typeof(CombatComboWaitForCompletionActionNode))]
[Serializable]
public sealed class CombatComboWaitForCompletionProvider : ActionDataProvider<CombatComboWaitForCompletionData>
{
}

[ActionNodeMenu("Combat/Combo Wait For Completion", "Waits until the current attack animation reports completion.")]
public sealed class CombatComboWaitForCompletionActionNode : CombatComboActionNode<CombatComboWaitForCompletionData>
{
    public CombatComboWaitForCompletionActionNode(ActionDataProvider<CombatComboWaitForCompletionData> dataProvider) : base(dataProvider)
    {
    }

    public override async Awaitable ExecuteAsync(ActionContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        CombatActionContext combatContext = RequireCombatContext(context);
        CombatComboWaitForCompletionData data = _selector.GetNext() ?? new CombatComboWaitForCompletionData();
        int expectedCompletedCount = combatContext.CurrentAttackIndex + 1;

        while (!ActionGraphBlackboardUtility.TryGet(context, data.completedCountKey, out int completedCount) ||
               completedCount < expectedCompletedCount)
        {
            token.ThrowIfCancellationRequested();
            await Awaitable.NextFrameAsync(token);
        }
    }
}

[Serializable]
public sealed class CombatComboAdvanceData
{
    [Min(1)] public int comboCount = 3;
    public string comboStepKey = "ComboStep";
}

[NodeBinding(typeof(CombatComboAdvanceActionNode))]
[Serializable]
public sealed class CombatComboAdvanceProvider : ActionDataProvider<CombatComboAdvanceData>
{
}

[ActionNodeMenu("Combat/Combo Advance If Input Accepted", "Advances to the next attack only after buffered input was accepted.")]
public sealed class CombatComboAdvanceActionNode : CombatComboActionNode<CombatComboAdvanceData>
{
    public CombatComboAdvanceActionNode(ActionDataProvider<CombatComboAdvanceData> dataProvider) : base(dataProvider)
    {
    }

    public override async Awaitable ExecuteAsync(ActionContext context, CancellationToken token)
    {
        await Awaitable.MainThreadAsync();
        token.ThrowIfCancellationRequested();

        CombatActionContext combatContext = RequireCombatContext(context);
        CombatComboAdvanceData data = _selector.GetNext() ?? new CombatComboAdvanceData();
        Blackboard blackboard = ActionGraphBlackboardUtility.RequireBlackboard(context);

        combatContext.AdvanceCombo(Mathf.Max(1, data.comboCount));

        if (!string.IsNullOrWhiteSpace(data.comboStepKey))
            blackboard.SetValue(data.comboStepKey, combatContext.CurrentAttackIndex);
    }
}

[Serializable]
public sealed class CombatComboInputAcceptedCondition : ICondition
{
    public bool expected = true;

    public bool Evaluate(ActionContext context)
    {
        return context is CombatActionContext combatContext &&
               combatContext.ComboInputAccepted == expected;
    }
}
