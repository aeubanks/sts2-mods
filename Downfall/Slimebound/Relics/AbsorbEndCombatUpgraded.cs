namespace Downfall.Slimebound.Relics;

/// <summary>
/// Black Heart of Goo (Touch of Orobas upgrade of Heart of Goo):
/// Whenever you consume Goop, heal 3 HP (up to 15 HP per combat).
/// Uses RelicRarity.Starter so it is automatically excluded from normal relic pools.
/// </summary>
public sealed class AbsorbEndCombatUpgraded : HeartOfGooRelic
{
    protected override int HealPerConsumeAmount => 3;
    protected override int MaxHealPerCombatAmount => 15;
}
