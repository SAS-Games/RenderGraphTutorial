using System;
using System.Collections.Generic;
using System.Threading;
using SAS.Pool;
using SAS.WeaponSystem.Components;
using UnityEngine;

namespace SAS.ActionGraph.WeaponSystem
{
    [Serializable]
    public class WeaponPooledProjectileData
    {
        public ComponentPoolSO<Poolable> objectPool;
        public ProjectileSpawnInfo[] spawnInfos = new ProjectileSpawnInfo[0];
        public int prewarmCount = 4;
    }

    [NodeBinding(typeof(SpawnPooledProjectilesNode))]
    [Serializable]
    public class WeaponPooledProjectileProvider : ActionDataProvider<WeaponPooledProjectileData>, IIndexedActionDataProvider
    {
    }

    [ActionNodeMenu("Weapon/Spawn Pooled Projectiles", "Spawns the configured projectiles from a shared pool at the attacker's source transform.")]
    public class SpawnPooledProjectilesNode : WeaponActionNode<WeaponPooledProjectileData>
    {
        private static readonly HashSet<int> InitializedPools = new HashSet<int>();

        public SpawnPooledProjectilesNode(ActionDataProvider<WeaponPooledProjectileData> dataProvider) : base(dataProvider)
        {
        }

        public override void Init(ActionContext context)
        {
            WeaponPooledProjectileData[] allData = _dataProvider.GetAllData();
            if (allData == null)
                return;

            for (int i = 0; i < allData.Length; i++)
            {
                WeaponPooledProjectileData data = allData[i];
                if (data == null || data.objectPool == null)
                    continue;

                int instanceId = data.objectPool.GetInstanceID();
                if (!InitializedPools.Add(instanceId))
                    continue;

                data.objectPool.Initialize(Mathf.Max(0, data.prewarmCount));
            }
        }

        public override async Awaitable ExecuteAsync(ActionContext context, CancellationToken token)
        {
            await Awaitable.MainThreadAsync();
            token.ThrowIfCancellationRequested();

            CombatActionContext combatContext = RequireCombatContext(context);
            WeaponPooledProjectileData data = GetAttackData(combatContext);
            if (data == null || data.objectPool == null || data.spawnInfos == null)
                return;

            Transform spawnTransform = GetProjectileSourceTransform(combatContext);
            if (spawnTransform == null)
                return;

            for (int i = 0; i < data.spawnInfos.Length; i++)
            {
                ProjectileSpawnInfo spawnInfo = data.spawnInfos[i];
                spawnInfo.SetRuntime(spawnTransform, combatContext.Owner);
                data.objectPool.Spawn(spawnInfo);
            }
        }

        private static Transform GetProjectileSourceTransform(CombatActionContext combatContext) =>
            combatContext.OriginTransform;
    }
}
