using System;
using System.Threading;
using UnityEngine;

[Serializable]
public class CombatPhaseActionData
{
    public CombatPhase phase = CombatPhase.CombatAttack;
}

[NodeBinding(typeof(CombatPhaseActionNode))]
[Serializable]
public class CombatPhaseActionProvider : ActionDataProvider<CombatPhaseActionData>
{
}

[ActionNodeMenu("Combat/Set Phase")]
public class CombatPhaseActionNode : ActionNode<CombatPhaseActionData>
{
    public CombatPhaseActionNode(ActionDataProvider<CombatPhaseActionData> dataProvider) : base(dataProvider)
    {
    }

    public override async Awaitable ExecuteAsync(ActionContext context, CancellationToken token)
    {
        await Awaitable.MainThreadAsync();
        token.ThrowIfCancellationRequested();

        CombatPhaseActionData data = _selector.GetNext();
        CombatStateController combatState = context?.Owner != null
            ? context.Owner.GetComponentInChildren<CombatStateController>(true)
            : null;
        if (data != null && combatState != null)
            combatState.SetPhase(data.phase);
    }
}
