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
/// Retract. Return 1 (2) card(s) from your Discard Pile to your Hand. Exhaust.
/// </summary>
public sealed class Rewind() : HexaCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        RetractHoverTip
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

        var discard = PileType.Discard.GetPile(Owner).Cards.ToList();
        if (discard.Count == 0)
            return;

        int count = Math.Min(DynamicVars.Cards.IntValue, Math.Min(room, discard.Count));
        List<CardModel> chosen;
        if (discard.Count <= count)
        {
            chosen = discard;
        }
        else
        {
            var prefs = new CardSelectorPrefs(SelectionScreenPrompt, count);
            chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, discard, Owner, prefs)).ToList();
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
