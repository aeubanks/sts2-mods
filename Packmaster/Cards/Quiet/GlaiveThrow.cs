using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Cards.Quiet;

public sealed class GlaiveThrow()
    : PackmasterCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "glaive_throw";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(7m, ValueProp.Move),
        new CardsVar(2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        int count = DynamicVars.Cards.IntValue;
        await CardPileCmd.Draw(choiceContext, count, Owner);

        int handCount = PileType.Hand.GetPile(Owner).Cards.Count;
        int toDiscard = Math.Min(count, handCount);
        if (toDiscard > 0)
        {
            var prefs = new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, toDiscard);
            var selected = await CardSelectCmd.FromHandForDiscard(choiceContext, Owner, prefs, null, this);
            await CardCmd.Discard(choiceContext, selected);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
