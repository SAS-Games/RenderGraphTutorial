using UnityEngine;

[DisallowMultipleComponent]
public sealed class FollowCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new(2.5f, 4f, 6f);
    [SerializeField] private Vector3 lookOffset = new(0f, 1f, 0f);
    [SerializeField, Min(0f)] private float followSharpness = 10f;

    private void Start()
    {
        ResolveTarget();
        SnapToTarget();
    }

    private void LateUpdate()
    {
        ResolveTarget();
        if (target == null)
            return;

        float blend = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, blend);
        transform.LookAt(target.position + lookOffset, Vector3.up);
    }

    private void ResolveTarget()
    {
        if (target != null)
            return;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            target = player.transform;
            return;
        }

        CharacterController character = FindFirstObjectByType<CharacterController>();
        if (character != null)
            target = character.transform;
    }

    private void SnapToTarget()
    {
        if (target == null)
            return;

        transform.position = target.position + offset;
        transform.LookAt(target.position + lookOffset, Vector3.up);
    }
}
