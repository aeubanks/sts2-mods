using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Awakened.Cards;

public sealed class Clutch()
    : AwakenedCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "clutch";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        var hand = PileType.Hand.GetPile(Owner);
        if (hand.Cards.Count >= CardPile.MaxCardsInHand
            || !Hook.ShouldDraw(CombatState, Owner, fromHandDraw: false, out _))
        {
            return;
        }

        var drawPile = PileType.Draw.GetPile(Owner);
        var zeroCost = drawPile.Cards
            .Where(c => !c.EnergyCost.CostsX
                && !c.Keywords.Contains(CardKeyword.Unplayable)
                && c.EnergyCost.GetWithModifiers(CostModifiers.All) == 0)
            .ToList();

        if (zeroCost.Count > 0)
        {
            var chosen = Owner.RunState.Rng.CombatCardSelection.NextItem(zeroCost);
            if (chosen is not null)
            {
                drawPile.MoveToTopInternal(chosen);
                await CardPileCmd.Draw(choiceContext, 1, Owner);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
