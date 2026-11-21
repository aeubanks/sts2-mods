using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Guardian.Cards;

public sealed class TwinSlam() : GuardianCard(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
{
    public override int MaxSockets => IsUpgraded ? 2 : 1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(7m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<SecondStrike>(upgrade: IsUpgraded),
        GemHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (CombatState is not null && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            var second = (SecondStrike)CombatState.CreateCard<SecondStrike>(Owner);
            if (IsUpgraded)
                CardCmd.Upgrade(second);
            second.CopySocketsFrom(this);
            await CardPileCmd.AddGeneratedCardToCombat(second, PileType.Hand, Owner);
        }

        await TriggerSocketedGemsAsync(choiceContext, cardPlay);
    }
}
