using System;
using SAS.StateMachineCharacterController;
using UnityEngine;

[DisallowMultipleComponent]
public class CombatStateController : MonoBehaviour
{
    [Serializable]
    private sealed class ActionPhaseDefinition
    {
        public string actionId;
        public CombatPhase startPhase;
        public CombatPhase releasePhase;

        public ActionPhaseDefinition(string actionId, CombatPhase startPhase, CombatPhase releasePhase)
        {
            this.actionId = actionId;
            this.startPhase = startPhase;
            this.releasePhase = releasePhase;
        }
    }

    [SerializeField] private CombatActionGraphController m_ActionController;
    [SerializeField] private FSMCharacterController m_CharacterController;
    [SerializeField] private AnimationController m_AnimationController;
    [SerializeField] private string m_CombatPhaseParameter = "CombatPhase";
    [SerializeField] private int m_MovementLockPriority = 100;
    [SerializeField] private ActionPhaseDefinition[] m_ActionPhases =
    {
        new ActionPhaseDefinition(SwordAndShieldActionIds.SwordCombo, CombatPhase.CombatAttack, CombatPhase.None),
        new ActionPhaseDefinition(SwordAndShieldActionIds.ShieldAttack, CombatPhase.CombatAttack, CombatPhase.None),
        new ActionPhaseDefinition(SwordAndShieldActionIds.HeavyAttack, CombatPhase.None, CombatPhase.CombatAttack),
        new ActionPhaseDefinition(SwordAndShieldActionIds.ShieldRush, CombatPhase.ShieldHold, CombatPhase.Rush)
    };

    private readonly object movementLockSource = new object();
    private IMovementVelocityComposer movementComposer;

    public CombatPhase CurrentPhase { get; private set; }
    public bool IsActive => CurrentPhase != CombatPhase.None;

    private void Awake()
    {
        if (m_ActionController == null)
            m_ActionController = GetComponent<CombatActionGraphController>();

        if (m_CharacterController == null)
            m_CharacterController = GetComponentInParent<FSMCharacterController>();

        if (m_AnimationController == null)
            m_AnimationController = GetComponentInParent<AnimationController>();

        movementComposer = m_CharacterController as IMovementVelocityComposer;
    }

    private void OnEnable()
    {
        if (m_ActionController == null)
            m_ActionController = GetComponent<CombatActionGraphController>();

        if (m_ActionController == null)
            return;

        m_ActionController.ActionStarted += HandleActionStarted;
        m_ActionController.ActionCompleted += HandleActionEnded;
        m_ActionController.ActionCancelled += HandleActionEnded;
        m_ActionController.ActionFailed += HandleActionFailed;
        m_ActionController.SignalPublished += HandleSignalPublished;
    }

    private void OnDisable()
    {
        if (m_ActionController != null)
        {
            m_ActionController.ActionStarted -= HandleActionStarted;
            m_ActionController.ActionCompleted -= HandleActionEnded;
            m_ActionController.ActionCancelled -= HandleActionEnded;
            m_ActionController.ActionFailed -= HandleActionFailed;
            m_ActionController.SignalPublished -= HandleSignalPublished;
        }

        ExitCombat();
    }

    public void SetPhase(CombatPhase phase)
    {
        if (CurrentPhase == phase)
            return;

        bool wasActive = IsActive;
        CurrentPhase = phase;
        m_CharacterController?.Actor.SetInteger(m_CombatPhaseParameter, (int)phase);

        if (wasActive != IsActive)
            SetMovementLocked(IsActive);
    }

    public void ExitCombat()
    {
        SetPhase(CombatPhase.None);
    }

    private void HandleActionStarted(string actionId)
    {
        if (TryGetDefinition(actionId, out ActionPhaseDefinition definition) && definition.startPhase != CombatPhase.None)
            SetPhase(definition.startPhase);
    }

    private void HandleSignalPublished(string actionId, string signalName)
    {
        if (!string.Equals(signalName, CombatGraphKeys.HoldReleased, StringComparison.Ordinal))
            return;

        if (TryGetDefinition(actionId, out ActionPhaseDefinition definition) && definition.releasePhase != CombatPhase.None)
            SetPhase(definition.releasePhase);
    }

    private void HandleActionEnded(string _)
    {
        ExitCombat();
    }

    private void HandleActionFailed(string _, Exception __)
    {
        ExitCombat();
    }

    private bool TryGetDefinition(string actionId, out ActionPhaseDefinition definition)
    {
        if (m_ActionPhases != null)
        {
            for (int i = m_ActionPhases.Length - 1; i >= 0; i--)
            {
                ActionPhaseDefinition candidate = m_ActionPhases[i];
                if (candidate != null && string.Equals(candidate.actionId, actionId, StringComparison.Ordinal))
                {
                    definition = candidate;
                    return true;
                }
            }
        }

        definition = null;
        return false;
    }

    private void SetMovementLocked(bool locked)
    {
        m_AnimationController?.SetCombatMovementLocked(locked);

        if (locked)
        {
            movementComposer?.SetMovementVelocityContribution(
                movementLockSource,
                Vector3.zero,
                MovementVelocityContributionMode.OverrideHorizontal,
                m_MovementLockPriority);
        }
        else
        {
            movementComposer?.ClearMovementVelocityContribution(movementLockSource);
        }
    }
}
