using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Cards;

public sealed class Rummage()
    : PackmasterCard(2, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "rummage";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(10m, ValueProp.Move),
        new CardsVar(1),
        new IntVar("CostReduction", 1m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        var drawnCards = await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        var reduction = DynamicVars["CostReduction"].IntValue;
        foreach (var drawn in drawnCards)
        {
            if (!drawn.EnergyCost.CostsX && drawn.EnergyCost.GetWithModifiers(CostModifiers.All) > 0)
            {
                drawn.EnergyCost.AddThisTurn(-reduction, reduceOnly: true);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
