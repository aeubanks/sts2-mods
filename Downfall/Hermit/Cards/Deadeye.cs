using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hermit.Cards;

public sealed class Deadeye()
    : HermitCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "deadeye";

    public override bool UsesDeadOn => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(5m, ValueProp.Move),
        new PowerVar<StrengthPower>(2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ..base.ExtraHoverTips,
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        int triggers = await ConsumeDeadOnTriggersAsync(cardPlay);
        if (triggers > 0)
        {
            await PowerCmd.Apply<StrengthPower>(
                choiceContext,
                Owner.Creature,
                DynamicVars.Strength.BaseValue * triggers,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m);
        DynamicVars.Strength.UpgradeValueBy(1m);
    }
}
