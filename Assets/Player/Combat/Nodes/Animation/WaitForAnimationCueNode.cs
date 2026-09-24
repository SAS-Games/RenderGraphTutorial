using System;
using System.Threading;
using UnityEngine;

namespace SAS.ActionGraph.WeaponSystem
{
    [Serializable]
    public class WeaponWaitForAnimationCueData
    {
        public string cueCountKey = "ProjectileCueCount";
        public float timeoutSeconds = -1f;
        public bool throwOnTimeout;
    }

    [NodeBinding(typeof(WaitForAnimationCueNode))]
    [Serializable]
    public class WeaponWaitForAnimationCueProvider : ActionDataProvider<WeaponWaitForAnimationCueData>, IIndexedActionDataProvider
    {
    }

    [ActionNodeMenu("Weapon/Wait For Animation Cue", "Waits for the current attack's named animation cue value on the graph blackboard.")]
    public class WaitForAnimationCueNode : WeaponActionNode<WeaponWaitForAnimationCueData>
    {
        public WaitForAnimationCueNode(ActionDataProvider<WeaponWaitForAnimationCueData> dataProvider) : base(dataProvider)
        {
        }

        public override async Awaitable ExecuteAsync(ActionContext context, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            CombatActionContext combatContext = RequireCombatContext(context);
            WeaponWaitForAnimationCueData data = GetAttackData(combatContext) ?? new WeaponWaitForAnimationCueData();
            if (string.IsNullOrWhiteSpace(data.cueCountKey))
                return;

            int expectedCueCount = combatContext.CurrentAttackIndex + 1;
            float deadline = data.timeoutSeconds < 0f ? float.PositiveInfinity : Time.time + data.timeoutSeconds;

            while (!ActionGraphBlackboardUtility.TryGet(context, data.cueCountKey, out int cueCount) ||
                   cueCount < expectedCueCount)
            {
                token.ThrowIfCancellationRequested();

                if (Time.time >= deadline)
                {
                    if (data.throwOnTimeout)
                        throw new TimeoutException($"Animation cue '{data.cueCountKey}' timed out for attack index {combatContext.CurrentAttackIndex}.");

                    return;
                }

                await Awaitable.NextFrameAsync(token);
            }
        }
    }
}
