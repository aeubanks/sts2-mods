using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Fetch a card from your Draw Pile into your Hand. Switch to a random other Ghostflame.
/// Exhaust. Cost: 1 (0).
/// </summary>
public sealed class SeventhEye() : HexaCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GhostflameHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var hand = PileType.Hand.GetPile(Owner);
        var draw = PileType.Draw.GetPile(Owner).Cards.ToList();
        if (draw.Count > 0 && hand.Cards.Count < CardPile.MaxCardsInHand)
        {
            var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
            var chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, draw, Owner, prefs)).FirstOrDefault();
            if (chosen is not null && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                await CardPileCmd.Add(chosen, PileType.Hand);
        }

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await HexaMechanics.SwitchToRandomOtherGhostflameAsync(choiceContext, Owner);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
