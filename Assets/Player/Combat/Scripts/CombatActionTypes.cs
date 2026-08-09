public enum CombatActionId
{
    None,
    SwordCombo,
    ShieldAttack,
    HeavyAttack,
    ShieldRush
}

public enum CombatComboInput
{
    None,
    Sword,
    Shield
}

public enum CombatPhase
{
    None = 0,
    CombatAttack = 1,
    ShieldHold = 2,
    Rush = 3
}

public static class CombatGraphKeys
{
    public const string ComboInput = "ComboInput";
    public const string ComboStep = "ComboStep";
    public const string ComboDecisionReady = "ComboDecisionReady";
    public const string AnimationEnded = "AnimationEnded";
    public const string HoldReleased = "HoldReleased";
    public const string RushHit = "RushHit";
    public const string MovementRequested = "MovementRequested";
}
