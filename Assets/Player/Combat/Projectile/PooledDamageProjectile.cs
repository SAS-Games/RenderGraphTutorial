using SAS.Pool;
using UnityEngine;

namespace SAS.WeaponSystem.Components
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Poolable))]
    [AddComponentMenu("SAS/Combat/Pooled Damage Projectile")]
    public sealed class PooledDamageProjectile : MonoBehaviour, ISpawnable
    {
        [SerializeField, Min(0f)] private float m_Speed = 15f;
        [SerializeField, Min(0f)] private float m_Lifetime = 5f;
        [SerializeField] private LayerMask m_HitLayers = ~0;
        [SerializeField] private bool m_DespawnWhenNoReceiver = true;
        [SerializeField] private Rigidbody m_Rigidbody;

        private Poolable poolable;
        private GameObject instigator;
        private float damage;
        private float remainingLifetime;
        private Vector3 travelDirection;
        private bool hasHit;

        private void Awake()
        {
            poolable = GetComponent<Poolable>();
            if (m_Rigidbody == null)
                m_Rigidbody = GetComponent<Rigidbody>();
        }

        public void OnSpawn(object data)
        {
            if (poolable == null)
                poolable = GetComponent<Poolable>();
            if (m_Rigidbody == null)
                m_Rigidbody = GetComponent<Rigidbody>();

            hasHit = false;
            remainingLifetime = m_Lifetime;

            if (data is not ProjectileSpawnInfo spawnInfo || spawnInfo.Origin == null)
            {
                Debug.LogWarning($"{nameof(PooledDamageProjectile)} requires {nameof(ProjectileSpawnInfo)} with an origin.", this);
                return;
            }

            instigator = spawnInfo.Instigator;
            damage = spawnInfo.Damage;

            Transform origin = spawnInfo.Origin;
            Vector3 directionOffset = origin.TransformDirection(spawnInfo.Direction);
            travelDirection = (origin.forward + directionOffset).normalized;
            if (travelDirection.sqrMagnitude < 0.0001f)
                travelDirection = origin.forward;

            transform.SetPositionAndRotation(origin.TransformPoint(spawnInfo.Offset), Quaternion.LookRotation(travelDirection, origin.up));

            if (m_Rigidbody != null)
            {
                m_Rigidbody.linearVelocity = travelDirection * m_Speed;
                m_Rigidbody.angularVelocity = Vector3.zero;
            }
        }

        public void OnDespawn()
        {
            if (m_Rigidbody != null)
            {
                m_Rigidbody.linearVelocity = Vector3.zero;
                m_Rigidbody.angularVelocity = Vector3.zero;
            }

            instigator = null;
            damage = 0f;
            hasHit = false;
        }

        private void Update()
        {
            if (m_Rigidbody == null && m_Speed > 0f)
                transform.position += travelDirection * (m_Speed * Time.deltaTime);

            if (m_Lifetime <= 0f)
                return;

            remainingLifetime -= Time.deltaTime;
            if (remainingLifetime <= 0f)
                poolable.Despawn();
        }

        private void OnTriggerEnter(Collider other)
        {
            TryHit(other.gameObject, other.ClosestPoint(transform.position));
        }

        private void OnCollisionEnter(Collision collision)
        {
            Vector3 hitPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            TryHit(collision.gameObject, hitPoint);
        }

        private void TryHit(GameObject target, Vector3 hitPoint)
        {
            if (hasHit || target == null || (m_HitLayers.value & (1 << target.layer)) == 0)
                return;

            if (instigator != null && target.transform.root == instigator.transform.root)
                return;

            IDamageReceiver receiver = FindDamageReceiver(target);
            if (receiver == null && !m_DespawnWhenNoReceiver)
                return;

            hasHit = true;
            receiver?.ReceiveDamage(new DamageInfo(damage, instigator, hitPoint, travelDirection));
            poolable.Despawn();
        }

        private static IDamageReceiver FindDamageReceiver(GameObject target)
        {
            MonoBehaviour[] behaviours = target.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IDamageReceiver receiver)
                    return receiver;
            }

            return null;
        }
    }
}
