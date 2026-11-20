using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Guardian.Cards;

public sealed class Clone() : GuardianCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        IsUpgraded
            ? [StasisHoverTip, AccelerateHoverTip]
            : [StasisHoverTip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hand = PileType.Hand.GetPile(Owner);
        if (hand.Cards.Count > 0 && GuardianMechanics.CanPlaceInStasis(Owner))
        {
            var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
            var selected = (await CardSelectCmd.FromHand(choiceContext, Owner, prefs, null, this)).FirstOrDefault();
            if (selected is not null && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            {
                var clone = selected.CreateClone();
                await GuardianMechanics.PlaceInStasisAsync(choiceContext, Owner, clone);
            }
        }

        if (IsUpgraded && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await GuardianMechanics.AccelerateAsync(choiceContext, Owner);
        }
    }
}
