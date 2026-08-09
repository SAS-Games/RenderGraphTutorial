using System.Collections.Generic;

/// <summary>
/// Builds small, reusable action graphs. Animation parameters live in these
/// graphs; runtime components only provide input signals and movement services.
/// </summary>
public static class CombatActionGraphDefinition
{
    private static readonly string[] CombatTriggers =
    {
        "Sword01", "Sword02", "Sword03", "Shield03", "ShieldAttack",
        "ShieldRush", "ShieldRushImpact", "HeavyAttack", "ReturnIdle"
    };

    public static NodeConfig Build(CombatActionId action)
    {
        return action switch
        {
            CombatActionId.SwordCombo => BuildSwordCombo(),
            CombatActionId.ShieldAttack => BuildSingleAttack("ShieldAttack"),
            CombatActionId.HeavyAttack => BuildHeavyAttack(),
            CombatActionId.ShieldRush => BuildShieldRush(),
            _ => null
        };
    }

    private static NodeConfig BuildSwordCombo()
    {
        return Sequence(
            SetInt(CombatGraphKeys.ComboStep, 0),
            new LoopNodeConfig
            {
                maxIterations = 2,
                conditionTiming = LoopConditionTiming.AfterChild,
                condition = StringEquals(CombatGraphKeys.ComboInput, CombatComboInput.Sword.ToString()),
                child = Sequence(
                    SetBool(CombatGraphKeys.ComboDecisionReady, false),
                    SequentialTriggers("Sword01", "Sword02"),
                    WaitFor(CombatGraphKeys.ComboDecisionReady),
                    IncrementInt(CombatGraphKeys.ComboStep))
            },
            Branch(All(
                    IntCompare(CombatGraphKeys.ComboStep, ActionGraphNumberComparison.GreaterOrEqual, 2),
                    Any(
                        StringEquals(CombatGraphKeys.ComboInput, CombatComboInput.Sword.ToString()),
                        StringEquals(CombatGraphKeys.ComboInput, CombatComboInput.Shield.ToString()))),
                BuildComboFinisher(),
                Trigger("ReturnIdle")));
    }

    private static NodeConfig BuildComboFinisher()
    {
        return Sequence(
            SetBool(CombatGraphKeys.AnimationEnded, false),
            SetBool(CombatGraphKeys.MovementRequested, false),
            Branch(StringEquals(CombatGraphKeys.ComboInput, CombatComboInput.Sword.ToString()),
                Trigger("Sword03"),
                Trigger("Shield03")),
            WaitForAnimationAndPush(1f, 0.4f),
            Trigger("ReturnIdle"));
    }

    private static NodeConfig BuildSingleAttack(string triggerName)
    {
        return Sequence(
            AttackWithPush(triggerName, 0.4f, 0.3f),
            Trigger("ReturnIdle"));
    }

    private static NodeConfig BuildHeavyAttack()
    {
        return Sequence(
            AnimatorBool("HeavyHold", true),
            WaitFor(CombatGraphKeys.HoldReleased),
            AnimatorBool("HeavyHold", false),
            AttackWithPush("HeavyAttack", 1.2f, 0.5f),
            Trigger("ReturnIdle"));
    }

    private static NodeConfig BuildShieldRush()
    {
        return Sequence(
            AnimatorBool("ShieldStance", true),
            WaitFor(CombatGraphKeys.HoldReleased),
            AnimatorBool("ShieldStance", false),
            Trigger("ShieldRush"),
            ShieldRush(25f, 1f),
            Branch(BoolEquals(CombatGraphKeys.RushHit, true),
                Sequence(
                    SetCombatPhase(CombatPhase.CombatAttack),
                    AttackWithPush("ShieldRushImpact", -1f, 0.5f)),
                null),
            Trigger("ReturnIdle"));
    }

    private static NodeConfig AttackWithPush(
        string triggerName,
        float distance,
        float duration)
    {
        return Sequence(
            SetBool(CombatGraphKeys.AnimationEnded, false),
            SetBool(CombatGraphKeys.MovementRequested, false),
            Trigger(triggerName),
            WaitForAnimationAndPush(distance, duration));
    }

    private static NodeConfig WaitForAnimationAndPush(float distance, float duration)
    {
        return Parallel(
            WaitFor(CombatGraphKeys.AnimationEnded),
            Sequence(
                WaitFor(CombatGraphKeys.MovementRequested),
                DirectionalPush(distance, duration)));
    }

    private static NodeConfig Trigger(string parameterName)
    {
        return ActionNode<AnimatorSetTriggerProvider, AnimatorSetTriggerData>(new AnimatorSetTriggerData
        {
            parameterName = parameterName,
            resetBeforeSet = CombatTriggers
        });
    }

    private static NodeConfig SequentialTriggers(params string[] parameterNames)
    {
        var data = new AnimatorSetTriggerData[parameterNames.Length];
        for (int i = 0; i < parameterNames.Length; i++)
        {
            data[i] = new AnimatorSetTriggerData
            {
                parameterName = parameterNames[i],
                resetBeforeSet = CombatTriggers
            };
        }

        var provider = new AnimatorSetTriggerProvider();
        provider.SetData(data, false);
        return new ActionNodeConfig { dataProvider = provider };
    }

    private static NodeConfig AnimatorBool(string parameterName, bool value)
    {
        return ActionNode<AnimatorSetBoolProvider, AnimatorSetBoolData>(new AnimatorSetBoolData
        {
            parameterName = parameterName,
            value = value
        });
    }

    private static NodeConfig DirectionalPush(float distance, float duration)
    {
        return ActionNode<CombatMovementActionProvider, CombatMovementActionData>(new CombatMovementActionData
        {
            actionType = CombatMovementActionType.DirectionalPush,
            distance = distance,
            duration = duration
        });
    }

    private static NodeConfig ShieldRush(float speed, float duration)
    {
        return ActionNode<CombatMovementActionProvider, CombatMovementActionData>(new CombatMovementActionData
        {
            actionType = CombatMovementActionType.ShieldRush,
            speed = speed,
            duration = duration,
            resultKey = CombatGraphKeys.RushHit
        });
    }

    private static NodeConfig SetCombatPhase(CombatPhase phase)
    {
        return ActionNode<CombatPhaseActionProvider, CombatPhaseActionData>(new CombatPhaseActionData
        {
            phase = phase
        });
    }

    private static NodeConfig WaitFor(string boolKey)
    {
        return ActionNode<WaitUntilConditionNodeProvider, WaitUntilConditionData>(new WaitUntilConditionData
        {
            condition = BoolEquals(boolKey, true),
            timeoutSeconds = -1f,
            throwOnTimeout = false
        });
    }

    private static NodeConfig SetBool(string key, bool value)
    {
        return ActionNode<ActionGraphSetBlackboardValueProvider, ActionGraphBlackboardSetValueData>(new ActionGraphBlackboardSetValueData
        {
            key = key,
            valueType = ActionGraphBlackboardValueType.Bool,
            boolValue = value
        });
    }

    private static NodeConfig SetInt(string key, int value)
    {
        return ActionNode<ActionGraphSetBlackboardValueProvider, ActionGraphBlackboardSetValueData>(new ActionGraphBlackboardSetValueData
        {
            key = key,
            valueType = ActionGraphBlackboardValueType.Int,
            intValue = value
        });
    }

    private static NodeConfig IncrementInt(string key)
    {
        return ActionNode<ActionGraphModifyBlackboardNumberProvider, ActionGraphBlackboardNumberData>(new ActionGraphBlackboardNumberData
        {
            key = key,
            numberType = ActionGraphBlackboardNumberType.Int,
            operation = ActionGraphBlackboardNumberOperation.Add,
            value = 1f,
            createIfMissing = true
        });
    }

    private static ActionNodeConfig ActionNode<TProvider, TData>(TData value)
        where TProvider : ActionDataProvider<TData>, new()
    {
        var provider = new TProvider();
        provider.SetData(new[] { value });
        return new ActionNodeConfig { dataProvider = provider };
    }

    private static FlowNodeConfig Sequence(params NodeConfig[] children)
    {
        return new FlowNodeConfig
        {
            type = FlowNodeType.Sequence,
            children = new List<NodeConfig>(children)
        };
    }

    private static FlowNodeConfig Parallel(params NodeConfig[] children)
    {
        return new FlowNodeConfig
        {
            type = FlowNodeType.Parallel,
            children = new List<NodeConfig>(children)
        };
    }

    private static ConditionNodeConfig Branch(ICondition condition, NodeConfig trueNode, NodeConfig falseNode)
    {
        return new ConditionNodeConfig
        {
            condition = condition,
            trueNode = trueNode,
            falseNode = falseNode
        };
    }

    private static ICondition BoolEquals(string key, bool value)
    {
        return new ActionGraphBlackboardBoolCondition
        {
            key = key,
            expected = value,
            resultWhenMissing = false
        };
    }

    private static ICondition StringEquals(string key, string value)
    {
        return new ActionGraphBlackboardStringCondition
        {
            key = key,
            comparison = ActionGraphStringComparison.Equal,
            compareValue = value,
            ignoreCase = false,
            resultWhenMissing = false
        };
    }

    private static ICondition IntCompare(string key, ActionGraphNumberComparison comparison, int value)
    {
        return new ActionGraphBlackboardIntCondition
        {
            key = key,
            comparison = comparison,
            compareValue = value,
            resultWhenMissing = false
        };
    }

    private static ICondition Any(params ICondition[] conditions)
    {
        return new CompositeCondition
        {
            mode = ActionGraphConditionMode.Any,
            conditions = conditions
        };
    }

    private static ICondition All(params ICondition[] conditions)
    {
        return new CompositeCondition
        {
            mode = ActionGraphConditionMode.All,
            conditions = conditions
        };
    }
}
