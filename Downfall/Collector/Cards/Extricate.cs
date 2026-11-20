using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Collector.Cards;

/// <summary>
/// Sever Soul (Extricate, Uncommon, 2 cost, Attack):
/// Exhaust all non-Attack cards in your hand. Deal 16 (22) damage.
/// </summary>
public sealed class Extricate() : CollectorCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(16m, ValueProp.Move)
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (Owner.PlayerCombatState is { } pcs)
        {
            var nonAttacks = pcs.Hand.Cards
                .Where(c => !ReferenceEquals(c, this) && c.Type != CardType.Attack)
                .ToList();

            foreach (var card in nonAttacks)
            {
                if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                    break;
                if (pcs.Hand.Cards.Contains(card))
                {
                    await CardCmd.Exhaust(choiceContext, card);
                }
            }
        }

        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var target = ResolveTarget(Owner, cardPlay.Target);
        if (target is not null)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(6m);
    }
}
