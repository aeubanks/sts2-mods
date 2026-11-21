using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Collector.Potions;

/// <summary>
/// Reserve Potion (Uncommon):
/// Gain 2 Reserves.
/// </summary>
public sealed class ReservePotion : CollectorPotion
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Reserves", 2m)
    ];

    public override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CollectorCard.ReservesHoverTip
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await CollectorMechanics.GainReservesAsync(choiceContext, Owner, DynamicVars["Reserves"].IntValue);
    }
}
