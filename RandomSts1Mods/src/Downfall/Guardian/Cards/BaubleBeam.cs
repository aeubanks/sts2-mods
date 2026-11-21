using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Guardian.Cards;

public sealed class BaubleBeam() : GuardianCard(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    public override int MaxSockets => IsUpgraded ? 2 : 1;
    public override bool IsBeam => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(10m, ValueProp.Move),
        new IntVar("Activations", 3m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GemHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt", null, "heavy_attack.mp3")
            .Execute(choiceContext);

        await TriggerSocketedGemsAsync(choiceContext, cardPlay, DynamicVars["Activations"].IntValue);
    }
}
