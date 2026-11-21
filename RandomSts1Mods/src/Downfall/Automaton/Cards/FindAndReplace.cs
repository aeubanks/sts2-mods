using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Find and Replace (Rare, 0 cost, Exhaust):
/// Fetch a card from your Draw or Discard Pile. Add a Dazed where it was (removed on Upgrade).
/// </summary>
public sealed class FindAndReplace() : BronzeCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => IsUpgraded
        ? []
        : [HoverTipFactory.FromCard<Dazed>()];

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        if (owner.PlayerCombatState is not { } pcs
            || owner.Creature.CombatState is not { } combatState
            || !owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var drawPile = pcs.DrawPile;
        var discardPile = pcs.DiscardPile;

        var candidates = new List<CardModel>(drawPile.Cards.Count + discardPile.Cards.Count);
        candidates.AddRange(drawPile.Cards);
        candidates.AddRange(discardPile.Cards);
        candidates.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));

        if (candidates.Count == 0)
            return;

        var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
        var chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, candidates, owner, prefs)).FirstOrDefault();
        if (chosen is null || !owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        bool fromDraw = drawPile.Cards.Contains(chosen);
        bool fromDiscard = !fromDraw && discardPile.Cards.Contains(chosen);
        if (!fromDraw && !fromDiscard)
            return;

        var dest = pcs.Hand.Cards.Count < CardPile.MaxCardsInHand ? PileType.Hand : PileType.Discard;
        await CardPileCmd.Add(chosen, dest);

        if (!IsUpgraded && owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            if (fromDraw)
            {
                await AutomatonMechanics.InsertCardIntoDrawPileAsync<Dazed>(owner, 1);
            }
            else
            {
                var dazed = combatState.CreateCard<Dazed>(owner);
                await CardPileCmd.AddGeneratedCardToCombat(dazed, PileType.Discard, owner);
            }
        }
    }
}
