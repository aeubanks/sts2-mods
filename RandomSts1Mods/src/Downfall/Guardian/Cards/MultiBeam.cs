using Downfall.Guardian.Orbs;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Guardian.Cards;

public sealed class MultiBeam() : GuardianCard(0, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies), IInStasisCard
{
    protected override bool HasEnergyCostX => true;
    public override bool IsBeam => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        new IntVar("TickBonus", 2m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is null)
            return;

        int hits = ResolveEnergyXValue();
        if (hits <= 0)
            return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(hits)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash", null, "heavy_attack.mp3")
            .Execute(choiceContext);
    }

    public Task OnTurnStartInStasisAsync(PlayerChoiceContext choiceContext, StasisOrb orb)
    {
        DynamicVars.Damage.BaseValue += DynamicVars["TickBonus"].BaseValue;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["TickBonus"].UpgradeValueBy(1m);
    }
}
