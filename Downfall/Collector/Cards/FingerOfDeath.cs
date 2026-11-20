using Downfall.Collector.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Cards;

/// <summary>
/// Finger of Death (Rare, 4 cost, Skill):
/// Only Reserves may be spent on this. Apply 60 Doom (Upgraded: to ALL enemies).
/// </summary>
public sealed class FingerOfDeath() : CollectorCard(4, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy), IReservesOnlyCard
{
    public override TargetType TargetType => IsUpgraded ? TargetType.AllEnemies : TargetType.AnyEnemy;

    protected override bool IsPlayable
    {
        get
        {
            if (IsMutable && Owner?.PlayerCombatState is not null)
            {
                int required = EnergyCost.CostsX
                    ? 0
                    : Math.Max(0, EnergyCost.GetWithModifiers(CostModifiers.All));
                if (CollectorMechanics.GetReserves(Owner) < required)
                    return false;
            }
            return base.IsPlayable;
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<CollectorDoomPower>(60m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ReservesHoverTip,
        DoomHoverTip
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (CombatState is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        decimal doom = DynamicVars["CollectorDoomPower"].BaseValue;
        if (IsUpgraded)
        {
            foreach (var enemy in CombatState.HittableEnemies.Where(e => e.IsAlive).ToList())
            {
                if (CombatManager.Instance.IsOverOrEnding)
                    break;
                if (!enemy.IsAlive)
                    continue;
                await CollectorMechanics.ApplyDoomAsync(choiceContext, enemy, doom, Owner.Creature, this);
            }
        }
        else
        {
            var target = ResolveTarget(Owner, cardPlay.Target);
            if (target is not null)
            {
                await CollectorMechanics.ApplyDoomAsync(choiceContext, target, doom, Owner.Creature, this);
            }
        }
    }
}
