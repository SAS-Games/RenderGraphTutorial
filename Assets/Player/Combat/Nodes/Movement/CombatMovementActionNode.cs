using System;
using System.Threading;
using System.Threading.Tasks;
using SAS.StateMachineCharacterController;
using UnityEngine;

public enum CombatMovementActionType
{
    DirectionalPush,
    ShieldRush
}

[Serializable]
public class CombatMovementActionData
{
    public CombatMovementActionType actionType;

    [Tooltip("Signed travel distance for DirectionalPush.")]
    public float distance = 1f;

    [Tooltip("Movement duration in seconds.")]
    public float duration = 0.25f;

    [Tooltip("Movement speed for ShieldRush.")]
    public float speed = 10f;

    [Tooltip("Blackboard bool written with the ShieldRush collision result.")]
    public string resultKey = CombatGraphKeys.RushHit;

    [Tooltip("Priority used by the FSM movement composer.")]
    public int priority = 200;
}

[NodeBinding(typeof(CombatMovementActionNode))]
[Serializable]
public class CombatMovementActionProvider : ActionDataProvider<CombatMovementActionData>
{
}

[ActionNodeMenu("Combat/Movement")]
public class CombatMovementActionNode : ActionNode<CombatMovementActionData>
{
    private readonly object movementSource = new object();

    public CombatMovementActionNode(ActionDataProvider<CombatMovementActionData> dataProvider) : base(dataProvider)
    {
    }

    public override async Awaitable ExecuteAsync(ActionContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        CombatMovementActionData data = _selector.GetNext();
        FSMCharacterController character = context?.Owner?.GetComponent<FSMCharacterController>();
        IMovementVelocityComposer movementComposer = character as IMovementVelocityComposer;
        if (data == null || character == null || movementComposer == null)
            return;

        try
        {
            switch (data.actionType)
            {
                case CombatMovementActionType.DirectionalPush:
                    await ApplyDirectionalPushAsync(data, character, movementComposer, token);
                    break;

                case CombatMovementActionType.ShieldRush:
                    bool hitSomething = await ApplyShieldRushAsync(data, character, movementComposer, token);
                    if (!string.IsNullOrEmpty(data.resultKey))
                        ActionGraphBlackboardUtility.RequireBlackboard(context).SetValue(data.resultKey, hitSomething);
                    break;
            }
        }
        finally
        {
            movementComposer.ClearMovementVelocityContribution(movementSource);
        }
    }

    private async Task ApplyDirectionalPushAsync(
        CombatMovementActionData data,
        FSMCharacterController character,
        IMovementVelocityComposer movementComposer,
        CancellationToken token)
    {
        float safeDuration = Mathf.Max(data.duration, 0.01f);
        float speed = data.distance / safeDuration;
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            token.ThrowIfCancellationRequested();
            movementComposer.SetMovementVelocityContribution(
                movementSource,
                character.MovementForward * speed,
                MovementVelocityContributionMode.OverrideHorizontal,
                data.priority);

            await Awaitable.NextFrameAsync();
            elapsed += Time.deltaTime;
        }
    }

    private async Task<bool> ApplyShieldRushAsync(
        CombatMovementActionData data,
        FSMCharacterController character,
        IMovementVelocityComposer movementComposer,
        CancellationToken token)
    {
        CharacterController collisionController = character.GetComponent<CharacterController>();
        float elapsed = 0f;

        while (elapsed < Mathf.Max(data.duration, 0.01f))
        {
            token.ThrowIfCancellationRequested();
            movementComposer.SetMovementVelocityContribution(
                movementSource,
                character.MovementForward * data.speed,
                MovementVelocityContributionMode.OverrideHorizontal,
                data.priority);

            await Awaitable.NextFrameAsync();
            elapsed += Time.deltaTime;

            if (collisionController != null &&
                (collisionController.collisionFlags & CollisionFlags.Sides) != 0)
                return true;
        }

        return false;
    }
}
