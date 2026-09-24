using SAS.Pool;
using UnityEngine;
using UnityEngine.VFX;

[CreateAssetMenu(menuName = "SAS/Pool/Factory/Visual Effect")]
public sealed class VisualEffectFactorySO : FactorySO<VisualEffect>
{
    [SerializeField] private VisualEffect m_VisualEffect;

    public override bool Create(string id, out VisualEffect item)
    {
        if (m_VisualEffect == null)
        {
            item = null;
            return false;
        }

        item = Instantiate(m_VisualEffect);
        return item != null;
    }
}