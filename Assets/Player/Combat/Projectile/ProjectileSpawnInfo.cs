using System;
using UnityEngine;

namespace SAS.WeaponSystem.Components
{
    [Serializable]
    public struct ProjectileSpawnInfo
    {
        [field: SerializeField] public Vector3 Offset { get; private set; }
        [field: SerializeField] public Vector3 Direction { get; private set; }
        [field: SerializeField, Min(0f)] public float Damage { get; private set; }

        public Transform Origin { get; private set; }
        public GameObject Instigator { get; private set; }

        public void SetRuntime(Transform origin, GameObject instigator)
        {
            Origin = origin;
            Instigator = instigator;
        }
    }
}
