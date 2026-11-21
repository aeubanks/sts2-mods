using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Ethereal. Afterlife. Deal 8 (10) damage. Deal 3 (4) additional damage for each other Ethereal card in Hand.
/// Afterlife: Deal 8 (10) damage to a random enemy (without the Hand-Ethereal bonus).
/// </summary>
public sealed class GhostLash() : HexaCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];
    public override bool HasAfterlife => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(8m),
        new ExtraDamageVar(3m),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier((card, _) =>
            card.Owner?.PlayerCombatState?.Hand.Cards.Count(c =>
                !ReferenceEquals(c, card) && c.Keywords.Contains(CardKeyword.Ethereal)) ?? 0)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        AfterlifeHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.CalculatedDamage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override async Task OnAfterlifeAsync(PlayerChoiceContext choiceContext, bool causedByEthereal)
    {
        var combatState = Owner.Creature.CombatState;
        if (combatState is null)
            return;

        var enemies = combatState.HittableEnemies.Where(e => e.IsAlive).ToList();
        var target = Owner.RunState.Rng.CombatTargets.NextItem(enemies);
        if (target is null)
            return;

        await CreatureCmd.Damage(
            choiceContext,
            target,
            DynamicVars.CalculationBase.BaseValue,
            ValueProp.Move,
            Owner.Creature,
            this,
            null);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.CalculationBase.UpgradeValueBy(2m);
        DynamicVars.ExtraDamage.UpgradeValueBy(1m);
    }
}
