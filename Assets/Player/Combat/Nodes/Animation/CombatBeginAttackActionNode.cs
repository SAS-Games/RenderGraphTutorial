using System;
using System.Threading;
using UnityEngine;

[Serializable]
public class CombatBeginAttackData
{
    public string triggerName = "Attack";
    [Tooltip("Optional animator triggers to clear before setting the attack trigger.")]
    public string[] resetBeforeSet;
}

[NodeBinding(typeof(CombatBeginAttackActionNode))]
[Serializable]
public class CombatBeginAttackActionProvider : ActionDataProvider<CombatBeginAttackData>
{
}

[ActionNodeMenu("Combat/Begin Attack")]
public class CombatBeginAttackActionNode : ActionNode<CombatBeginAttackData>
{
    public CombatBeginAttackActionNode(ActionDataProvider<CombatBeginAttackData> dataProvider) : base(dataProvider)
    {
    }

    public override async Awaitable ExecuteAsync(ActionContext context, CancellationToken token)
    {
        await Awaitable.MainThreadAsync();
        token.ThrowIfCancellationRequested();

        var blackboard = ActionGraphBlackboardUtility.RequireBlackboard(context);
        blackboard.SetValue(CombatGraphKeys.AnimationEnded, false);
        blackboard.SetValue(CombatGraphKeys.MovementRequested, false);

        CombatBeginAttackData data = _selector.GetNext();
        if (data == null || string.IsNullOrEmpty(data.triggerName) || context.Owner == null)
            return;

        Animator animator = context.Owner.GetComponentInParent<Animator>();
        if (animator == null)
            animator = context.Owner.GetComponentInChildren<Animator>();

        if (animator == null)
            return;

        if (data.resetBeforeSet != null)
        {
            foreach (string triggerName in data.resetBeforeSet)
            {
                if (!string.IsNullOrEmpty(triggerName))
                    animator.ResetTrigger(triggerName);
            }
        }

        animator.SetTrigger(data.triggerName);
    }
}
