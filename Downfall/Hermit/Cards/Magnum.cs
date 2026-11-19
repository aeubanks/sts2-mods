using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hermit.Cards;

public sealed class Magnum()
    : HermitCard(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    private const int DiscardCount = 6;

    protected override string PortraitKey => "magnum";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        var handCards = PileType.Hand.GetPile(Owner).Cards.Where(c => !ReferenceEquals(c, this)).ToList();
        if (handCards.Count == 0)
            return;

        List<CardModel> toDiscard;
        if (handCards.Count <= DiscardCount)
        {
            toDiscard = handCards;
        }
        else
        {
            var prefs = new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, DiscardCount);
            toDiscard = (await CardSelectCmd.FromHandForDiscard(choiceContext, Owner, prefs, c => !ReferenceEquals(c, this), this)).ToList();
        }

        if (toDiscard.Count == 0)
            return;

        await CardCmd.Discard(choiceContext, toDiscard);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(toDiscard.Count)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
