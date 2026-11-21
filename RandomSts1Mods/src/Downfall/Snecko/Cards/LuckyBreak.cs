using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Snecko.Cards;

public sealed class LuckyBreak() : SneckoCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(8m, ValueProp.Move),
        new CardsVar(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        int count = cardPlay.Resources.EnergyValue >= 2 ? 1 : 0;
        foreach (var card in PileType.Hand.GetPile(Owner).Cards)
        {
            if (ReferenceEquals(card, this) || card.EnergyCost.CostsX)
                continue;
            if (card.EnergyCost.GetWithModifiers(CostModifiers.All) >= 2)
                count++;
        }

        int totalDraw = count * DynamicVars.Cards.IntValue;
        if (totalDraw > 0)
        {
            await CardPileCmd.Draw(choiceContext, totalDraw, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
