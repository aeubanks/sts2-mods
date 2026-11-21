using Downfall.Slimebound.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Slimebound.Cards;

public sealed class ViciousTackle() : SlimeCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override bool IsTackle => true;
    public override bool UsesConsumeGlow => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(17m, ValueProp.Move),
        new IntVar("SelfDamage", 3m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        TackleHoverTip,
        GoopHoverTip
    ];

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (!ReferenceEquals(cardSource, this)
            || target is null
            || Owner?.Creature is not Creature ownerCreature
            || dealer != ownerCreature
            || !props.IsPoweredAttack())
        {
            return 0m;
        }

        // SlimedPower.ModifyDamageAdditive already adds +1x Goop; add a second +1x here for 2x Goop damage.
        return target.GetPowerAmount<SlimedPower>();
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        await SlimeMechanics.ResolveTackleSelfDamageAsync(choiceContext, Owner, DynamicVars["SelfDamage"].IntValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(7m);
    }
}
