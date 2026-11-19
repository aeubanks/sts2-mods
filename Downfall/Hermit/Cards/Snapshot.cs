using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hermit.Cards;

public sealed class Snapshot()
    : HermitCard(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "snapshot";

    public override bool UsesDeadOn => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        bool deadOn = IsDeadOnNow;

        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        if (deadOn)
        {
            decimal unblocked = attack.Results.SelectMany(r => r).Sum(r => r.UnblockedDamage);
            if (unblocked > 0m)
            {
                await CreatureCmd.GainBlock(Owner.Creature, unblocked, ValueProp.Unpowered, cardPlay);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
