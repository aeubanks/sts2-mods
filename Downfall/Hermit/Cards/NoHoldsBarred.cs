using Downfall.Hermit.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hermit.Cards;

public sealed class NoHoldsBarred()
    : HermitCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    protected override string PortraitKey => "no_holds_barred";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(19m, ValueProp.Move),
        new PowerVar<BruisePower>(5m),
        new EnergyVar(1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BruisePower>(),
        HoverTipFactory.FromPower<HermitDrainedPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        decimal bruise = DynamicVars[nameof(BruisePower)].BaseValue;
        foreach (var enemy in CombatState.HittableEnemies.ToList())
        {
            await PowerCmd.Apply<BruisePower>(choiceContext, enemy, bruise, Owner.Creature, this);
        }

        await PowerCmd.Apply<HermitDrainedPower>(choiceContext, Owner.Creature, DynamicVars.Energy.IntValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
        DynamicVars[nameof(BruisePower)].UpgradeValueBy(1m);
    }
}
