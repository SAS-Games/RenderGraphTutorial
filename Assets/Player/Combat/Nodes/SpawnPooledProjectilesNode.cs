using System;
using System.Collections.Generic;
using System.Threading;
using SAS.Pool;
using SAS.StateMachineCharacterController;
using SAS.WeaponSystem.Components;
using UnityEngine;

namespace SAS.WeaponSystem.Components
{
    [Serializable]
    public struct ProjectileSpawnInfo
    {
        [field: SerializeField] public Vector3 Offset { get; private set; }
        [field: SerializeField] public Vector3 Direction { get; private set; }
        public Transform Transform { get; private set; }

        public void SetTransform(Transform transform)
        {
            Transform = transform;
        }
    }
}

namespace SAS.ActionGraph.WeaponSystem
{
    [Serializable]
    public class WeaponWaitForAnimationCueData
    {
        public string cueCountKey = "ProjectileCueCount";
        public float timeoutSeconds = -1f;
        public bool throwOnTimeout;
    }

    [NodeBinding(typeof(WaitForAnimationCueNode))]
    [Serializable]
    public class WeaponWaitForAnimationCueProvider : ActionDataProvider<WeaponWaitForAnimationCueData>, IIndexedActionDataProvider
    {
    }

    [ActionNodeMenu("Weapon/Wait For Animation Cue", "Waits for the current attack's named animation cue value on the graph blackboard.")]
    public class WaitForAnimationCueNode : WeaponActionNode<WeaponWaitForAnimationCueData>
    {
        public WaitForAnimationCueNode(ActionDataProvider<WeaponWaitForAnimationCueData> dataProvider) : base(dataProvider)
        {
        }

        public override async Awaitable ExecuteAsync(ActionContext context, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            CombatActionContext combatContext = RequireCombatContext(context);
            WeaponWaitForAnimationCueData data = GetAttackData(combatContext) ?? new WeaponWaitForAnimationCueData();
            if (string.IsNullOrWhiteSpace(data.cueCountKey))
                return;

            int expectedCueCount = combatContext.CurrentAttackIndex + 1;
            float deadline = data.timeoutSeconds < 0f ? float.PositiveInfinity : Time.time + data.timeoutSeconds;

            while (!ActionGraphBlackboardUtility.TryGet(context, data.cueCountKey, out int cueCount) ||
                   cueCount < expectedCueCount)
            {
                token.ThrowIfCancellationRequested();

                if (Time.time >= deadline)
                {
                    if (data.throwOnTimeout)
                        throw new TimeoutException($"Animation cue '{data.cueCountKey}' timed out for attack index {combatContext.CurrentAttackIndex}.");

                    return;
                }

                await Awaitable.NextFrameAsync(token);
            }
        }
    }

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
                spawnInfo.SetTransform(spawnTransform);
                data.objectPool.Spawn(spawnInfo);
            }

            return;
        }

        private static Transform GetProjectileSourceTransform(CombatActionContext combatContext)
        {
            if (combatContext.OriginTransform != null)
                return combatContext.OriginTransform;

            if (combatContext.Owner != null)
            {
                ICharacter character = combatContext.Owner.GetComponentInParent<ICharacter>();
                if (character != null)
                    return character.Transform;
            }

            return combatContext.OriginTransform;
        }
    }
}
