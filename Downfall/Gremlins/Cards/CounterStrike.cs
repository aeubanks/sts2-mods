using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Gremlins.Cards;

public sealed class CounterStrike()
    : GremlinCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    protected override bool ShouldGlowGoldInternal =>
        IsMutable && (CombatState ?? Owner?.Creature.CombatState)?.HittableEnemies.Any(e => e.IsAlive && e.Monster is { IntendsToAttack: true }) == true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move),
        new RepeatVar(2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        bool intendsToAttack = cardPlay.Target.Monster is { IntendsToAttack: true };

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (intendsToAttack
            && CombatState is not null
            && Owner.Creature.IsAlive
            && !CombatManager.Instance.IsOverOrEnding
            && !GremlinMob.IsNob(CombatState, Owner))
        {
            GremlinKind kind = GremlinMob.GetActiveKind(CombatState, Owner);
            await GremlinMob.TriggerBonusAsync(Owner, CombatState, choiceContext, kind, DynamicVars.Repeat.IntValue);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars.Repeat.UpgradeValueBy(1m);
    }
}
