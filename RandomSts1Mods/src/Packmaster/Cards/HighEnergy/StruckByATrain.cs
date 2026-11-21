using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Cards.HighEnergy;

public sealed class StruckByATrain()
    : PackmasterCard(3, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    protected override string PortraitKey => "struck_by_a_train";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(24m, ValueProp.Move),
        new IntVar("DamageGain", 24m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var frontmost = CombatState!.HittableEnemies.FirstOrDefault();
        if (frontmost is not null)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(frontmost)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(choiceContext);
        }

        DynamicVars.Damage.BaseValue += DynamicVars["DamageGain"].BaseValue;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
        DynamicVars["DamageGain"].UpgradeValueBy(4m);
    }
}
