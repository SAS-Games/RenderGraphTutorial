using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.VFX;

namespace SAS.ActionGraph.WeaponSystem
{
    [Serializable]
    public class WeaponPooledVisualEffectData
    {
        public VisualEffectPoolSO effectPool;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
        [Min(0f)] public float returnDelay = 2f;
        public int prewarmCount = 2;
    }

    [NodeBinding(typeof(PlayPooledVisualEffectNode))]
    [Serializable]
    public class WeaponPooledVisualEffectProvider : ActionDataProvider<WeaponPooledVisualEffectData>, IIndexedActionDataProvider
    {
    }

    [ActionNodeMenu("Weapon/Play Pooled Visual Effect", "Spawns and plays a pooled VFX Graph VisualEffect at the combat origin.")]
    public class PlayPooledVisualEffectNode : WeaponActionNode<WeaponPooledVisualEffectData>
    {
        private static readonly HashSet<int> InitializedPools = new HashSet<int>();

        public PlayPooledVisualEffectNode(ActionDataProvider<WeaponPooledVisualEffectData> dataProvider) : base(dataProvider)
        {
        }

        public override void Init(ActionContext context)
        {
            WeaponPooledVisualEffectData[] allData = _dataProvider.GetAllData();
            if (allData == null)
                return;

            for (int i = 0; i < allData.Length; i++)
            {
                VisualEffectPoolSO pool = allData[i]?.effectPool;
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
            WeaponPooledVisualEffectData data = GetAttackData(combatContext);
            Transform origin = combatContext.OriginTransform;
            if (data?.effectPool == null || origin == null)
                return;

            VisualEffect effect = data.effectPool.Spawn();
            if (effect == null)
                return;

            effect.transform.SetPositionAndRotation(origin.TransformPoint(data.localPosition), origin.rotation * Quaternion.Euler(data.localEulerAngles));
            effect.transform.localScale = data.localScale;
            effect.Reinit();
            effect.Play();
            ReturnToPoolAfterDelay(data.effectPool, effect, data.returnDelay);
        }

        private static async void ReturnToPoolAfterDelay(VisualEffectPoolSO pool, VisualEffect effect, float delay)
        {
            try
            {
                float deadline = Time.time + Mathf.Max(0f, delay);
                do
                {
                    await Awaitable.NextFrameAsync();
                }
                while (effect != null && Time.time < deadline);

                if (pool == null || effect == null)
                    return;

                effect.Stop();
                pool.Despawn(effect);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
