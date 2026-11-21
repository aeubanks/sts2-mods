using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Guardian.Cards;

public sealed class CurlUp() : GuardianCard(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Brace", 10m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        StasisHoverTip,
        BraceHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.PlayerCombatState is { } pcs
            && pcs.Hand.Cards.Count > 0
            && GuardianMechanics.CanPlaceInStasis(Owner))
        {
            if (IsUpgraded)
            {
                var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
                var chosen = (await CardSelectCmd.FromHand(choiceContext, Owner, prefs, _ => true, this)).FirstOrDefault();
                if (chosen is not null)
                {
                    await GuardianMechanics.PlaceInStasisAsync(choiceContext, Owner, chosen);
                }
            }
            else
            {
                var hand = pcs.Hand.Cards.ToList();
                var randomCard = hand[Owner.RunState.Rng.CombatCardGeneration.NextInt(hand.Count)];
                await GuardianMechanics.PlaceInStasisAsync(choiceContext, Owner, randomCard);
            }
        }

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await GuardianMechanics.BraceAsync(choiceContext, Owner, DynamicVars["Brace"].IntValue);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Brace"].UpgradeValueBy(2m);
    }
}
