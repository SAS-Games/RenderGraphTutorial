using System;
using System.Threading;
using System.Threading.Tasks;
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

    public override Task ExecuteAsync(ActionContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var blackboard = ActionGraphBlackboardUtility.RequireBlackboard(context);
        blackboard.SetValue(CombatGraphKeys.AnimationEnded, false);
        blackboard.SetValue(CombatGraphKeys.MovementRequested, false);

        CombatBeginAttackData data = _selector.GetNext();
        if (data == null || string.IsNullOrEmpty(data.triggerName) || context.Owner == null)
            return Task.CompletedTask;

        Animator animator = context.Owner.GetComponentInParent<Animator>();
        if (animator == null)
            animator = context.Owner.GetComponentInChildren<Animator>();

        if (animator == null)
            return Task.CompletedTask;

        if (data.resetBeforeSet != null)
        {
            foreach (string triggerName in data.resetBeforeSet)
            {
                if (!string.IsNullOrEmpty(triggerName))
                    animator.ResetTrigger(triggerName);
            }
        }

        animator.SetTrigger(data.triggerName);
        return Task.CompletedTask;
    }
}
