using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hermit.Cards;

public sealed class ItchyTrigger()
    : HermitCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "itchy_trigger";

    public override bool UsesDeadOn => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(7m, ValueProp.Move),
        new IntVar("CostReduction", 1m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        bool deadOn = IsDeadOnNow;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        if (deadOn)
        {
            var eligible = PileType.Hand.GetPile(Owner).Cards
                .Where(c => !ReferenceEquals(c, this)
                    && !c.EnergyCost.CostsX
                    && !c.Keywords.Contains(CardKeyword.Unplayable)
                    && c.EnergyCost.GetWithModifiers(CostModifiers.All) > 0)
                .ToList();

            if (eligible.Count > 0)
            {
                var chosen = Owner.RunState.Rng.CombatCardSelection.NextItem(eligible);
                if (chosen is not null)
                {
                    chosen.EnergyCost.AddThisTurn(-DynamicVars["CostReduction"].IntValue, reduceOnly: true);
                }
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["CostReduction"].UpgradeValueBy(1m);
    }
}
