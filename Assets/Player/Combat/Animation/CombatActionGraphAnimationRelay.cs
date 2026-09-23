using System;
using UniRx;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatActionGraphAnimationRelay : MonoBehaviour
{
    [Serializable]
    private sealed class StateIntegerSignalBinding
    {
        public string actionId;
        public string stateName;
        public string blackboardKey;
        public int completedValue;
        public bool cancelActionOnInterruption = true;
    }

    [SerializeField] private CombatActionGraphController actionController;
    [SerializeField] private Animator animator;
    [SerializeField] private StateIntegerSignalBinding[] stateIntegerSignals;

    private CompositeDisposable stateSubscriptions;

    private void Awake()
    {
        if (actionController == null)
            actionController = transform.root.GetComponentInChildren<CombatActionGraphController>(true);

        if (actionController == null)
            Debug.LogError("Combat animation relay could not find a CombatActionGraphController.", this);

        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
            Debug.LogError("Combat animation relay requires an Animator on the same GameObject.", this);
    }

    private void OnEnable()
    {
        SubscribeToAnimatorStates();
    }

    private void OnDisable()
    {
        stateSubscriptions?.Dispose();
        stateSubscriptions = null;
    }

    public void Signal(string signalName)
    {
        actionController?.Signal(signalName);
    }

    public void EndCombo()
    {
        Signal("AnimationEnded");
    }

    public void RequestMovement(int _)
    {
        Signal("MovementRequested");
    }

    private void SubscribeToAnimatorStates()
    {
        stateSubscriptions?.Dispose();
        stateSubscriptions = new CompositeDisposable();

        if (animator == null || stateIntegerSignals == null)
            return;

        foreach (StateIntegerSignalBinding binding in stateIntegerSignals)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.stateName) || string.IsNullOrWhiteSpace(binding.blackboardKey))
            {
                continue;
            }

            StateIntegerSignalBinding capturedBinding = binding;

            try
            {
                animator.OnStateCompletedAsObservable(capturedBinding.stateName).Subscribe(_ => PublishCompletedState(capturedBinding)).AddTo(stateSubscriptions);

                if (capturedBinding.cancelActionOnInterruption)
                    animator.OnStateInterruptedAsObservable(capturedBinding.stateName).Subscribe(_ => CancelInterruptedAction(capturedBinding)).AddTo(stateSubscriptions);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    private void PublishCompletedState(StateIntegerSignalBinding binding)
    {
        if (!IsBindingActionActive(binding))
            return;

        actionController.SetBlackboardValue(binding.blackboardKey, binding.completedValue);
    }

    private void CancelInterruptedAction(StateIntegerSignalBinding binding)
    {
        if (IsBindingActionActive(binding))
            actionController.CancelCurrentAction();
    }

    private bool IsBindingActionActive(StateIntegerSignalBinding binding)
    {
        return actionController != null && actionController.IsBusy && (string.IsNullOrWhiteSpace(binding.actionId) || string.Equals(actionController.CurrentActionId, binding.actionId, StringComparison.Ordinal));
    }
}
