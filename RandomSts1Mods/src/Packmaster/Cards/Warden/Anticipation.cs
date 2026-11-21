using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Packmaster.Cards.Warden;

public sealed class Anticipation()
    : PackmasterCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "anticipation";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var drawn = (await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner)).ToList();

        int strength = 0;
        foreach (var card in drawn)
        {
            int cost = GetLogicalCardCost(card);
            if (IsUpgraded)
                strength += cost;
            else if (cost > strength)
                strength = cost;
        }

        if (strength > 0 && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, strength, Owner.Creature, this);
        }
    }

    private int GetLogicalCardCost(CardModel card)
    {
        if (card.Keywords.Contains(CardKeyword.Unplayable))
            return 0;

        if (card.EnergyCost.CostsX)
            return Math.Max(0, Owner.PlayerCombatState?.Energy ?? 0);

        if (card.EnergyCost.Canonical < 0)
            return 0;

        return Math.Max(0, card.EnergyCost.GetWithModifiers(CostModifiers.All));
    }
}
