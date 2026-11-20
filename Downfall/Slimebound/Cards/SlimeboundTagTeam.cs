using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Slimebound.Cards;

public sealed class SlimeboundTagTeam() : SlimeCard(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(12m, ValueProp.Move),
        new RepeatVar(2)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SplitHoverTip,
        CommandHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        if (IsUpgraded)
            await SlimeMechanics.SpawnSpecialistSlimeAsync(choiceContext, Owner);
        else
            await SlimeMechanics.SpawnNormalSlimeAsync(choiceContext, Owner);

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await SlimeMechanics.CommandAsync(choiceContext, Owner, DynamicVars.Repeat.IntValue);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(1m);
    }
}
