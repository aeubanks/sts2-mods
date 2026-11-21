using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Collector.Cards;

/// <summary>
/// Fiend Fire (CollectorFiendFire, Rare, 2 cost, Attack, Exhaust):
/// Exhaust your hand. Deal 7 (10) damage for each card Exhausted.
/// </summary>
public sealed class CollectorFiendFire() : CollectorCard(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(7m, ValueProp.Move)
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (Owner.PlayerCombatState is not { } pcs)
            return;

        int exhaustedCount = 0;
        var toExhaust = pcs.Hand.Cards.Where(c => !ReferenceEquals(c, this)).ToList();
        foreach (var card in toExhaust)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            if (pcs.Hand.Cards.Contains(card))
            {
                await CardCmd.Exhaust(choiceContext, card);
                exhaustedCount++;
            }
        }

        if (exhaustedCount <= 0 || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var target = ResolveTarget(Owner, cardPlay.Target);
        if (target is not null)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .WithHitCount(exhaustedCount)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
