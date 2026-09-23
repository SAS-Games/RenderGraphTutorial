using System;

namespace SAS.ActionGraph.WeaponSystem
{
public static class WeaponAttackDataSelector
{
    public static T GetForAttack<T>(T[] values, int attackIndex)
    {
        if (values == null || values.Length == 0)
            return default;

        int index = Math.Max(0, attackIndex);
        if (index >= values.Length)
            index = values.Length - 1;

        return values[index];
    }
}
}
