using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace SAS.ActionGraph.WeaponSystem
{
    [Serializable]
    public class WeaponPooledVfxData
    {
        public ParticleSystemPoolSO effectPool;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
        public int prewarmCount = 4;
    }

    [NodeBinding(typeof(PlayPooledVfxNode))]
    [Serializable]
    public class WeaponPooledVfxProvider : ActionDataProvider<WeaponPooledVfxData>, IIndexedActionDataProvider
    {
    }

    [ActionNodeMenu("Weapon/Play Pooled VFX", "Spawns a pooled particle effect at the combat origin and returns it after playback.")]
    public class PlayPooledVfxNode : WeaponActionNode<WeaponPooledVfxData>
    {
        private static readonly HashSet<int> InitializedPools = new HashSet<int>();

        public PlayPooledVfxNode(ActionDataProvider<WeaponPooledVfxData> dataProvider) : base(dataProvider)
        {
        }

        public override void Init(ActionContext context)
        {
            WeaponPooledVfxData[] allData = _dataProvider.GetAllData();
            if (allData == null)
                return;

            for (int i = 0; i < allData.Length; i++)
            {
                ParticleSystemPoolSO pool = allData[i]?.effectPool;
                if (pool == null || !InitializedPools.Add(pool.GetInstanceID()))
                    continue;

                pool.Initialize(Mathf.Max(0, allData[i].prewarmCount));
            }
        }

        public override async Awaitable ExecuteAsync(ActionContext context, CancellationToken token)
        {
            await Awaitable.MainThreadAsync();
            token.ThrowIfCancellationRequested();

            CombatActionContext combatContext = RequireCombatContext(context);
            WeaponPooledVfxData data = GetAttackData(combatContext);
            Transform origin = combatContext.OriginTransform;
            if (data?.effectPool == null || origin == null)
                return;

            ParticleSystem effect = data.effectPool.Spawn();
            if (effect == null)
                return;

            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.transform.SetPositionAndRotation(origin.TransformPoint(data.localPosition), origin.rotation * Quaternion.Euler(data.localEulerAngles));
            effect.transform.localScale = data.localScale;
            effect.Play(true);
            ReturnToPoolWhenFinished(data.effectPool, effect);
        }

        private static async void ReturnToPoolWhenFinished(ParticleSystemPoolSO pool, ParticleSystem effect)
        {
            try
            {
                await Awaitable.NextFrameAsync();
                while (effect != null && effect.IsAlive(true))
                    await Awaitable.NextFrameAsync();

                if (pool != null && effect != null)
                    pool.Despawn(effect);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
