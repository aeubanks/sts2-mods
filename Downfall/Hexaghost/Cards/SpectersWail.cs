using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Ethereal. Afterlife. Deal 4 (6) damage to ALL enemies twice.
/// Afterlife: Deal 4 (6) damage to ALL enemies once.
/// </summary>
public sealed class SpectersWail() : HexaCard(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];
    public override bool HasAfterlife => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        AfterlifeHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var combatState = Owner.Creature.CombatState;
        if (combatState is null)
            return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(2)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(combatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override async Task OnAfterlifeAsync(PlayerChoiceContext choiceContext, bool causedByEthereal)
    {
        var combatState = Owner.Creature.CombatState;
        if (combatState is null)
            return;

        var enemies = combatState.HittableEnemies.Where(e => e.IsAlive).ToList();
        if (enemies.Count == 0)
            return;

        await CreatureCmd.Damage(
            choiceContext,
            enemies,
            DynamicVars.Damage.BaseValue,
            ValueProp.Move,
            Owner.Creature,
            this,
            null);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
