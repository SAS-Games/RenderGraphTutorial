using System;
using System.Threading;
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
    public CollectBufferedInputCountActionNode(ActionDataProvider<CollectBufferedInputCountData> dataProvider)
        : base(dataProvider)
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
        if (!ActionGraphBlackboardUtility.TryGet(
                context,
                ActionGraphInputBuffer.BlackboardKey,
                out ActionGraphInputBuffer inputBuffer) || inputBuffer == null)
        {
            throw new InvalidOperationException(
                "Collect Buffered Input Count requires an ActionGraphInputBuffer registered on the graph blackboard.");
        }

        Animator animator = ResolveAnimator(context);
        int animatorParameterHash = string.IsNullOrWhiteSpace(data.animatorParameterName)
            ? 0
            : Animator.StringToHash(data.animatorParameterName);

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

    private static void ApplyCount(
        SAS.Core.BlackboardSystem.Blackboard blackboard,
        Animator animator,
        int animatorParameterHash,
        string outputKey,
        int count)
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
        if (!ActionGraphBlackboardUtility.TryGet(context, completedCountKey, out int completedCount) ||
            !ActionGraphBlackboardUtility.TryGet(context, queuedCountKey, out int queuedCount))
        {
            return resultWhenMissing;
        }

        return queuedCount > 0 && completedCount >= queuedCount;
    }
}
