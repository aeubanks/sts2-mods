using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Cards.EnergyAndEcho;

public sealed class TearApart()
    : PackmasterCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "tear_apart";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(17m, ValueProp.Move),
        new EnergyVar(1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        EchoHelper.EchoHoverTips.Append(HoverTipFactory.ForEnergy(this));

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        if (attack.Results.SelectMany(r => r).Any(r => r.WasTargetKilled))
        {
            await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
        }

        await EchoHelper.AddEchoCopyAsync(this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(5m);
    }
}
