using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Snecko.Cards;

public sealed class DefensiveFlair() : SneckoCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool GainsBlock => true;
    public override bool IsGift => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(8m, ValueProp.Move),
        new IntVar("BonusBlock", 2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        OffclassHoverTip,
        GiftHoverTip
    ];

    public override decimal ModifyBlockAdditive(
        Creature target,
        decimal block,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (cardSource != this
            || CombatState is null
            || Owner?.PlayerCombatState is null
            || target != Owner.Creature
            || !props.IsPoweredCardOrMonsterMoveBlock()
            || Pile?.Type is not (PileType.Hand or PileType.Play))
        {
            return 0m;
        }

        int offclassCount = PileType.Hand.GetPile(Owner).Cards.Count(c => !ReferenceEquals(c, this) && SneckoOffclass.IsOffclass(c, Owner));
        return offclassCount * DynamicVars["BonusBlock"].BaseValue;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override Task OnGiftClaimedAsync() =>
        SneckoOffclass.OfferOffclassDeckChoiceAsync(Owner, 3, c => c.Rarity == CardRarity.Uncommon);

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1m);
        DynamicVars["BonusBlock"].UpgradeValueBy(1m);
    }
}
