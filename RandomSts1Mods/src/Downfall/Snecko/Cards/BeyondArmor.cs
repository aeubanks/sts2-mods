using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Snecko.Cards;

public sealed class BeyondArmor() : SneckoCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool GainsBlock => true;
    public override bool IsGift => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(5m, ValueProp.Move),
        new CardsVar(2)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        OffclassHoverTip,
        GiftHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        if (Owner.PlayerCombatState is not { } pcs)
            return;

        var candidates = pcs.DrawPile.Cards
            .Where(c => SneckoOffclass.IsOffclass(c, Owner))
            .ToList();

        var rng = Owner.RunState.Rng.CombatCardSelection;
        int toMove = DynamicVars.Cards.IntValue;
        for (int i = 0; i < toMove && candidates.Count > 0; i++)
        {
            if (pcs.Hand.Cards.Count >= CardPile.MaxCardsInHand)
                break;

            int idx = rng.NextInt(candidates.Count);
            var picked = candidates[idx];
            candidates.RemoveAt(idx);
            await CardPileCmd.Add(picked, PileType.Hand);
        }
    }

    protected override Task OnGiftClaimedAsync() =>
        SneckoOffclass.OfferOffclassDeckChoiceAsync(Owner, 3, c => c.Rarity == CardRarity.Common);

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
