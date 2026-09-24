using SAS.Pool;
using UnityEngine;
using UnityEngine.VFX;

[CreateAssetMenu(menuName = "SAS/Pool/Visual Effect")]
public sealed class VisualEffectPoolSO : ComponentPoolSO<VisualEffect>
{
    [SerializeField] private VisualEffectFactorySO m_Factory;
    protected override IFactory<VisualEffect> Factory => m_Factory;
}
