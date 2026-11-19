using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Packmaster.Powers;

namespace Packmaster.Cards.Distortion;

public sealed class Static()
    : PackmasterCard(1, CardType.Attack, CardRarity.Rare, TargetType.RandomEnemy)
{
    protected override string PortraitKey => "static";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(1m, ValueProp.Move),
        new RepeatVar(4)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<DistortionPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is null)
            return;

        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(DynamicVars.Repeat.IntValue)
            .FromCard(this, cardPlay)
            .TargetingRandomOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        foreach (var result in attack.Results.SelectMany(r => r))
        {
            if (CombatManager.Instance.IsOverOrEnding || !Owner.Creature.IsAlive)
                break;

            if (result.UnblockedDamage > 0 && result.Receiver.IsAlive)
            {
                await PowerCmd.Apply<DistortionPower>(
                    choiceContext,
                    result.Receiver,
                    result.UnblockedDamage,
                    Owner.Creature,
                    this);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(1m);
    }
}
