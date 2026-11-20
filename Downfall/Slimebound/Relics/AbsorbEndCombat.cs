using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Slimebound.Relics;

public abstract class HeartOfGooRelic : SlimeRelic, IGoopConsumedListener
{
    private bool _inCombat;
    private int _remainingHeal;

    protected abstract int HealPerConsumeAmount { get; }
    protected abstract int MaxHealPerCombatAmount { get; }

    public override RelicRarity Rarity => RelicRarity.Starter;
    public override bool ShowCounter => _inCombat;
    public override int DisplayAmount => _remainingHeal;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HealVar(HealPerConsumeAmount),
        new IntVar("Cap", MaxHealPerCombatAmount)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SlimeCard.GoopHoverTip
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _inCombat = false;
        _remainingHeal = 0;
    }

    public override Task BeforeCombatStart()
    {
        _inCombat = true;
        _remainingHeal = MaxHealPerCombatAmount;
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _inCombat = false;
        _remainingHeal = 0;
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public async Task OnGoopConsumedAsync(PlayerChoiceContext choiceContext, Creature target, int consumedAmount)
    {
        if (!_inCombat || _remainingHeal <= 0 || !Owner.Creature.IsAlive)
            return;

        int heal = Math.Min(HealPerConsumeAmount, _remainingHeal);
        _remainingHeal -= heal;
        if (_remainingHeal <= 0)
        {
            Status = RelicStatus.Disabled;
        }
        InvokeDisplayAmountChanged();
        Flash();
        await CreatureCmd.Heal(Owner.Creature, heal);
    }
}

/// <summary>
/// Heart of Goo (Starter): Whenever you consume Goop, heal 2 HP (up to 8 HP per combat).
/// Upgrades via Touch of Orobas into Black Heart of Goo (AbsorbEndCombatUpgraded).
/// </summary>
public sealed class AbsorbEndCombat : HeartOfGooRelic
{
    protected override int HealPerConsumeAmount => 2;
    protected override int MaxHealPerCombatAmount => 8;

    public override RelicModel GetUpgradeReplacement() =>
        ModelDb.Relic<AbsorbEndCombatUpgraded>();
}
