using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Guardian.Cards;

public sealed class PrismaticBeam() : GuardianCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override int MaxSockets => 3;
    public override bool IsBeam => true;
    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GemHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        int gemCount = GetSocketedGems().Count;
        if (gemCount > 0)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .WithHitCount(gemCount)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash", null, "heavy_attack.mp3")
                .Execute(choiceContext);
        }

        await TriggerSocketedGemsAsync(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m);
    }
}
