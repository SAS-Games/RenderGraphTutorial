using UnityEngine;

namespace SAS.WeaponSystem.Components
{
    public readonly struct DamageInfo
    {
        public float Amount { get; }
        public GameObject Instigator { get; }
        public Vector3 HitPoint { get; }
        public Vector3 Direction { get; }

        public DamageInfo(float amount, GameObject instigator, Vector3 hitPoint, Vector3 direction)
        {
            Amount = amount;
            Instigator = instigator;
            HitPoint = hitPoint;
            Direction = direction;
        }
    }

    public interface IDamageReceiver
    {
        void ReceiveDamage(DamageInfo damageInfo);
    }
}
