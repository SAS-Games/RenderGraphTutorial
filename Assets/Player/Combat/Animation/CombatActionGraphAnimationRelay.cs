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

    [SerializeField] private CombatActionGraphController m_ActionController;
    [SerializeField] private Animator m_Animator;
    [SerializeField] private StateIntegerSignalBinding[] m_StateIntegerSignals;

    private CompositeDisposable stateSubscriptions;

    private void Awake()
    {
        if (m_ActionController == null)
            m_ActionController = transform.root.GetComponentInChildren<CombatActionGraphController>(true);

        if (m_ActionController == null)
            Debug.LogError("Combat animation relay could not find a CombatActionGraphController.", this);

        if (m_Animator == null)
            m_Animator = GetComponent<Animator>();

        if (m_Animator == null)
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
        m_ActionController?.Signal(signalName);
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

        if (m_Animator == null || m_StateIntegerSignals == null)
            return;

        foreach (StateIntegerSignalBinding binding in m_StateIntegerSignals)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.stateName) || string.IsNullOrWhiteSpace(binding.blackboardKey))
            {
                continue;
            }

            StateIntegerSignalBinding capturedBinding = binding;

            try
            {
                m_Animator.OnStateCompletedAsObservable(capturedBinding.stateName).Subscribe(_ => PublishCompletedState(capturedBinding)).AddTo(stateSubscriptions);

                if (capturedBinding.cancelActionOnInterruption)
                    m_Animator.OnStateInterruptedAsObservable(capturedBinding.stateName).Subscribe(_ => CancelInterruptedAction(capturedBinding)).AddTo(stateSubscriptions);
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

        m_ActionController.SetBlackboardValue(binding.blackboardKey, binding.completedValue);
    }

    private void CancelInterruptedAction(StateIntegerSignalBinding binding)
    {
        if (IsBindingActionActive(binding))
            m_ActionController.CancelCurrentAction();
    }

    private bool IsBindingActionActive(StateIntegerSignalBinding binding)
    {
        return m_ActionController != null && m_ActionController.IsBusy && (string.IsNullOrWhiteSpace(binding.actionId) || string.Equals(m_ActionController.CurrentActionId, binding.actionId, StringComparison.Ordinal));
    }
}
