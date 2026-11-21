using Downfall.Awakened.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Awakened.Cards;

public sealed class Thunderbolt()
    : AwakenedSpellCard(1, CardType.Attack, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "thunderbolt";

    private bool HasEyeOfTheOccult =>
        IsMutable && Owner is not null && Owner.Relics.OfType<EyeOfTheOccult>().Any();

    public override TargetType TargetType =>
        HasEyeOfTheOccult ? TargetType.AllEnemies : TargetType.AnyEnemy;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(12m, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is not null && Owner.Relics.OfType<EyeOfTheOccult>().FirstOrDefault() is { } eye)
        {
            if (CombatState.HittableEnemies.Count > 1)
            {
                eye.TriggerFlash();
            }

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .TargetingAllOpponents(CombatState)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
            return;
        }

        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(6m);
    }
}
