using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Guardian.Cards;

public sealed class RefractedBeam() : GuardianCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override int MaxSockets => 1;
    public override bool IsBeam => true;
    public override int MaxUpgradeLevel => 99;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new RepeatVar(4)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GemHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(DynamicVars.Repeat.IntValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash", null, "heavy_attack.mp3")
            .Execute(choiceContext);

        await TriggerSocketedGemsAsync(choiceContext, cardPlay, CurrentUpgradeLevel + 1);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(1m);
    }
}
