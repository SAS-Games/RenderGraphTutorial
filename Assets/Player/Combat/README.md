# Combat ActionGraph module

## Core

`Core/CombatActionGraphController.cs` is the reusable action runner. It owns
the ActionGraph executor, graph blackboard, input buffer registration, action
lifecycle, and generic blackboard signals. It has no dependency on Sword,
Shield, Heavy Attack, combat phases, character state, or legacy combo rules.

Actions are identified by strings and map directly to `ActionGraphAsset`
instances. Call `TryStart(actionId)` to start an action, `PublishInput(name)`
while one is running, or `Submit(actionId, inputName)` for both behaviours.

Runtime state is available through `IsBusy`, `CurrentActionId`, and the
`ActionStarted`, `ActionCompleted`, `ActionCancelled`, and `ActionFailed`
events. The same state is published to `Combat.ActionRunning` and
`Combat.CurrentAction` on the graph blackboard.

## Integrations

- `Input/ActionGraphInputBuffer.cs` stores runtime input events for graphs.
- `Input/CombatAttackCommand.cs` adapts the current `InputHandler` command API.
- `Input/CombatInputBinding.cs` maps input actions to graph action IDs without
  weapon-specific rules.
- `Extensions/SwordAndShield/Input/SwordAndShieldCombatInputBinding.cs`
  contains the hold, Heavy Attack, and Shield-specific input policy.
- `Animation/CombatActionGraphAnimationRelay.cs` forwards generic bool and int
  animation signals to the controller blackboard.
- `State/CombatStateController.cs` is an optional lifecycle extension. Its
  action-to-phase mappings are not part of the core controller.
- `Extensions/SwordAndShield/` contains the legacy combo-window integration,
  action IDs, phases, and showcase-specific blackboard keys.

## Nodes

`Nodes/` contains optional combat nodes grouped by responsibility. The counter
combo uses `CollectBufferedInputCountActionNode` and
`BufferedInputCompletionCondition` from `Nodes/Input/`.

Only broadly reusable ActionGraph primitives remain in `SASPackages-Core`.

## Standalone counter combo

To reuse `CounterSwordComboActionGraph` in another project, copy:

- `Core/CombatActionGraphController.cs`
- `Input/ActionGraphInputBuffer.cs`
- `Input/CombatAttackCommand.cs`
- `Input/CombatInputBinding.cs`
- `Animation/CombatActionGraphAnimationRelay.cs`
- `Nodes/Input/CollectBufferedInputCountActionNode.cs`
- `Graphs/CounterCombo/CounterSwordComboActionGraph.asset`

The host GameObject needs `CombatActionGraphController`,
`ActionGraphBlackboardComponent`, `ActionGraphInputBuffer`, and
`CombatInputBinding`. Register the graph under an action ID, then configure the
binding with `PrimaryAttack`, the same action ID, and the `Started` submit phase.
The binding calls `Submit(actionId, inputActionName)`, so the first press starts
the graph and later presses are automatically published to its input buffer.
In the included Player prefab that action ID is `SwordCombo`.

The Animator needs an integer parameter named `QueuedAttackCount`. Attach a
`TaggedObservableStateMachineTrigger` to attacks one, two, and three, assign a
stable tag to each state, and configure its completion threshold. Configure the
`CombatActionGraphAnimationRelay` beside the Animator to map those tags to
`CompletedAttackCount` values `1`, `2`, and `3`. No clip Animation Event is
required for combo completion.

The counter graph has no dependency on `SwordAndShieldCombatSignals` or
`CombatStateController`.
