using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Collector.Cards.Collectibles;

/// <summary>
/// Sentry (Collectible, 1 cost, Attack, Exhaust):
/// Deal 8 (10) damage and apply 1 (2) Weak. Repeat if there is a Sentry in your Exhaust Pile.
/// </summary>
public sealed class SentryCard() : CollectibleCard(1, CardType.Attack, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move),
        new PowerVar<WeakPower>(1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>()
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (Owner.PlayerCombatState is not { } pcs || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var target = ResolveTarget(Owner, cardPlay.Target);
        if (target is null)
            return;

        int times = pcs.ExhaustPile.Cards.Any(c => c is SentryCard) ? 2 : 1;
        for (int i = 0; i < times; i++)
        {
            if (!Owner.Creature.IsAlive || !target.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .Execute(choiceContext);

            if (target.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                await PowerCmd.Apply<WeakPower>(choiceContext, target, DynamicVars.Weak.BaseValue, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars.Weak.UpgradeValueBy(1m);
    }
}
