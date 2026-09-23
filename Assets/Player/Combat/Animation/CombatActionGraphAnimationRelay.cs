using System;
using UniRx;
using UnityEngine;
using UnityEngine.Serialization;

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

    [Serializable]
    private sealed class StateCueIntegerSignalBinding
    {
        public string actionId;
        public string stateName;
        public string cueName = "Projectile";
        public string blackboardKey = "ProjectileCueCount";
        public int publishedValue;
    }

    [FormerlySerializedAs("actionController")] [SerializeField] private CombatActionGraphController m_ActionController;
    [FormerlySerializedAs("animator")] [SerializeField] private Animator m_Animator;
    [FormerlySerializedAs("stateIntegerSignals")] [SerializeField] private StateIntegerSignalBinding[] m_StateIntegerSignals;
    [SerializeField] private StateCueIntegerSignalBinding[] m_StateCueIntegerSignals;

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

        if (m_Animator == null)
            return;

        if (m_StateIntegerSignals != null)
        {
            foreach (StateIntegerSignalBinding binding in m_StateIntegerSignals)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.stateName) || string.IsNullOrWhiteSpace(binding.blackboardKey))
                    continue;

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

        if (m_StateCueIntegerSignals == null)
            return;

        foreach (StateCueIntegerSignalBinding binding in m_StateCueIntegerSignals)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.stateName) ||
                string.IsNullOrWhiteSpace(binding.cueName) || string.IsNullOrWhiteSpace(binding.blackboardKey))
            {
                continue;
            }

            StateCueIntegerSignalBinding capturedBinding = binding;

            try
            {
                m_Animator.OnStateCueAsObservable(capturedBinding.stateName, capturedBinding.cueName)
                    .Subscribe(_ => PublishCue(capturedBinding))
                    .AddTo(stateSubscriptions);
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

    private void PublishCue(StateCueIntegerSignalBinding binding)
    {
        if (IsBindingActionActive(binding.actionId))
            m_ActionController.SetBlackboardValue(binding.blackboardKey, binding.publishedValue);
    }

    private bool IsBindingActionActive(StateIntegerSignalBinding binding)
    {
        return IsBindingActionActive(binding.actionId);
    }

    private bool IsBindingActionActive(string actionId)
    {
        return m_ActionController != null && m_ActionController.IsBusy &&
               (string.IsNullOrWhiteSpace(actionId) ||
                string.Equals(m_ActionController.CurrentActionId, actionId, StringComparison.Ordinal));
    }
}
