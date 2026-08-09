using SAS.Core.TagSystem;
using SAS.StateMachineCharacterController;
using UnityEngine;

/// <summary>
/// Locomotion animation bridge. Combat animation parameters are authored and
/// executed by ActionGraphs, so this component only maintains movement speed.
/// </summary>
[DefaultExecutionOrder(100)]
public class AnimationController : MonoBehaviour
{
    [SerializeField] private string m_SpeedParam = "Speed";

    [FieldRequiresChild] private Animator _animator;
    [FieldRequiresSelf] private FSMCharacterController _characterController;

    private bool _combatMovementLocked;
    private int _speedParamHash;

    private void Awake()
    {
        this.Initialize();
        _animator ??= GetComponentInChildren<Animator>(true);
        _characterController ??= GetComponent<FSMCharacterController>();
        _speedParamHash = Animator.StringToHash(m_SpeedParam);
    }

    private void Update()
    {
        // InputHandler runs at the default order (0). This component runs at 100,
        // so Speed is sampled after input and before Mecanim evaluates in
        // PreLateUpdate. LateUpdate is too late for the current animation frame.
        UpdateMovementSpeed();
    }

    private void UpdateMovementSpeed()
    {
        if (_animator == null || !_animator.isActiveAndEnabled)
            return;

        float speed = _combatMovementLocked || _characterController == null ? 0f : _characterController.Speed;
        _animator.SetFloat(_speedParamHash, speed);
    }

    public void SetCombatMovementLocked(bool locked)
    {
        _combatMovementLocked = locked;
    }
}
