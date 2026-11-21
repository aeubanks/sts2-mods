using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Nightmare Vision (RecurringNightmare): Retract. Return 1 (2) Ethereal card(s) from your Exhaust Pile to your Hand.
/// Exhaust. Cannot be generated in combat.
/// </summary>
public sealed class RecurringNightmare() : HexaCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool CanBeGeneratedInCombat => false;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        RetractHoverTip,
        HoverTipFactory.FromKeyword(CardKeyword.Ethereal)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await HexaMechanics.RetractAsync(choiceContext, Owner);

        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var hand = PileType.Hand.GetPile(Owner);
        int room = CardPile.MaxCardsInHand - hand.Cards.Count;
        if (room <= 0)
            return;

        var ethereals = PileType.Exhaust.GetPile(Owner).Cards
            .Where(c => c.Keywords.Contains(CardKeyword.Ethereal))
            .ToList();
        if (ethereals.Count == 0)
            return;

        int count = Math.Min(DynamicVars.Cards.IntValue, Math.Min(room, ethereals.Count));
        List<CardModel> chosen;
        if (ethereals.Count <= count)
        {
            chosen = ethereals;
        }
        else
        {
            var prefs = new CardSelectorPrefs(SelectionScreenPrompt, count);
            chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, ethereals, Owner, prefs)).ToList();
        }

        foreach (var card in chosen)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await CardPileCmd.Add(card, PileType.Hand);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
