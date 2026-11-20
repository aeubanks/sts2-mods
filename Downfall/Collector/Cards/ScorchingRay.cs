using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Collector.Cards;

/// <summary>
/// Scorching Ray (Common, X cost, Attack):
/// Deal 8 (11) damage to a random enemy X times.
/// </summary>
public sealed class ScorchingRay() : CollectorCard(0, CardType.Attack, CardRarity.Common, TargetType.RandomEnemy)
{
    protected override bool HasEnergyCostX => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move)
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (CombatState is null)
            return;

        int hits = ResolveEnergyXValue();
        if (hits <= 0)
            return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(hits)
            .FromCard(this, cardPlay)
            .TargetingRandomOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
