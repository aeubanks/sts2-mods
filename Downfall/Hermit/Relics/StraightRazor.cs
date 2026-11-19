using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Hermit.Relics;

public sealed class StraightRazor : HermitRelic
{
    protected override string IconKey => "straight_razor";

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HealVar(15m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Transform)
    ];

    public override async Task BeforeCardRemoved(CardModel card)
    {
        if (card.Owner != Owner || card.Pile?.Type != PileType.Deck || !Owner.Creature.IsAlive)
            return;

        Flash();
        await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue);
    }

    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (card.Owner != Owner
            || oldPileType != PileType.Deck
            || card.Pile?.Type != PileType.Deck
            || !Owner.Creature.IsAlive)
        {
            return;
        }

        Flash();
        await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue);
    }
}
