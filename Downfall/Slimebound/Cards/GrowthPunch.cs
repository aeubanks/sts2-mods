using Downfall.Slimebound.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Slimebound.Cards;

public sealed class GrowthPunch() : SlimeCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override bool GainsBlock => true;
    public override bool UsesConsumeGlow => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(4m, ValueProp.Move),
        new DamageVar(4m, ValueProp.Move),
        new IntVar("Bonus", 4m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ConsumeHoverTip,
        GoopHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        bool hadGoop = cardPlay.Target.HasPower<SlimedPower>();

        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        if (hadGoop)
        {
            decimal bonus = DynamicVars["Bonus"].BaseValue;
            DynamicVars.Block.BaseValue += bonus;
            DynamicVars.Damage.BaseValue += bonus;
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1m);
        DynamicVars.Damage.UpgradeValueBy(1m);
        DynamicVars["Bonus"].UpgradeValueBy(1m);
    }
}
