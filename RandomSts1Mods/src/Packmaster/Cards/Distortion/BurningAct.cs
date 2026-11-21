using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Packmaster.Cards.Distortion;

public sealed class BurningAct()
    : PackmasterCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private const int ExhaustCount = 2;

    protected override string PortraitKey => "burning_act";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(4)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var drawn = (await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner)).ToList();
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var hand = PileType.Hand.GetPile(Owner);
        var inHand = drawn.Where(c => c.Pile == hand).ToList();
        if (inHand.Count == 0)
            return;

        List<CardModel> toExhaust;
        if (inHand.Count <= ExhaustCount)
        {
            toExhaust = inHand;
        }
        else if (IsUpgraded)
        {
            var prefs = new CardSelectorPrefs(SelectionScreenPrompt, ExhaustCount);
            toExhaust = (await CardSelectCmd.FromHand(choiceContext, Owner, prefs, inHand.Contains, this)).ToList();
        }
        else
        {
            var rng = Owner.RunState.Rng.CombatCardSelection;
            toExhaust = new List<CardModel>(ExhaustCount);
            for (int i = 0; i < ExhaustCount && inHand.Count > 0; i++)
            {
                int idx = rng.NextInt(inHand.Count);
                toExhaust.Add(inHand[idx]);
                inHand.RemoveAt(idx);
            }
        }

        foreach (var card in toExhaust)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            if (card.Pile == hand)
            {
                await CardCmd.Exhaust(choiceContext, card);
            }
        }
    }
}
