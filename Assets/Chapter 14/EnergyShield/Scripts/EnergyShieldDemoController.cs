using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class EnergyShieldDemoController : MonoBehaviour
{
    [SerializeField] private CharacterController character;
    [SerializeField] private Transform shieldProxy;
    [SerializeField] private Vector3 followOffset = new(0f, 1.05f, 0f);
    [SerializeField] private bool shieldOnStart = true;

    public bool IsShieldActive { get; private set; }

    private void Awake()
    {
        if (character == null)
            character = FindFirstObjectByType<CharacterController>();

        if (character == null || shieldProxy == null)
        {
            Debug.LogError("Energy Shield demo requires a character and shield proxy.", this);
            enabled = false;
            return;
        }

        FollowCharacter();
        SetShieldActive(shieldOnStart);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            SetShieldActive(!IsShieldActive);
    }

    private void LateUpdate()
    {
        FollowCharacter();
    }

    public void SetShieldActive(bool active)
    {
        IsShieldActive = active;

        if (shieldProxy != null)
            shieldProxy.gameObject.SetActive(active);
    }

    private void FollowCharacter()
    {
        if (character == null || shieldProxy == null)
            return;

        shieldProxy.SetPositionAndRotation(
            character.transform.position + followOffset,
            character.transform.rotation);
    }
}
