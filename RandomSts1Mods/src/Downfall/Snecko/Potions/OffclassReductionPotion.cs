using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace Downfall.Snecko.Potions;

public sealed class OffclassReductionPotion : SneckoPotion
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("CostReduction", 1m)
    ];

    public override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SneckoCard.OffclassHoverTip
    ];

    protected override Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        int reduction = DynamicVars["CostReduction"].IntValue;
        foreach (var card in PileType.Hand.GetPile(Owner).Cards.ToList())
        {
            if (SneckoOffclass.IsOffclass(card, Owner) && !card.EnergyCost.CostsX && card.EnergyCost.Canonical >= 0)
            {
                card.EnergyCost.AddThisCombat(-reduction, reduceOnly: true);
                NCard.FindOnTable(card)?.PlayRandomizeCostAnim();
            }
        }
        return Task.CompletedTask;
    }
}
