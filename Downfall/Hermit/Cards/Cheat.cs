using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hermit.Cards;

public sealed class Cheat()
    : HermitCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "cheat";

    public override bool UsesDeadOn => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(3)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        bool deadOn = IsDeadOnNow;

        var drawCards = PileType.Draw.GetPile(Owner).Cards;
        if (drawCards.Count == 0)
            return;

        var topCards = drawCards.Take(DynamicVars.Cards.IntValue).ToList();
        var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
        var chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, topCards, Owner, prefs)).FirstOrDefault();
        if (chosen is null)
            return;

        if (chosen.TargetType == TargetType.AnyEnemy && CombatState.HittableEnemies.Count == 0)
            return;

        if (deadOn && chosen is HermitCard { UsesDeadOn: true } hermitCard)
        {
            hermitCard.ForceDeadOnOnce();
        }

        try
        {
            var target = chosen.TargetType == TargetType.AnyEnemy
                ? Owner.RunState.Rng.CombatTargets.NextItem(CombatState.HittableEnemies)
                : null;
            await CardCmd.AutoPlay(choiceContext, chosen, target);
        }
        finally
        {
            (chosen as HermitCard)?.ClearForcedDeadOn();
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(2m);
    }
}
