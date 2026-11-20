using Downfall.Collector.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Collector.Cards;

/// <summary>
/// Rotwood Kindling (Uncommon, Unplayable, Skill):
/// When Exhausted, apply 2 (3) Vulnerable and 4 (6) Doom to ALL enemies.
/// </summary>
public sealed class RotwoodKindling() : CollectorCard(-1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<VulnerablePower>(2m),
        new PowerVar<CollectorDoomPower>(4m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<VulnerablePower>(),
        DoomHoverTip
    ];

    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card != this || Owner is null || CombatState is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        decimal vuln = DynamicVars.Vulnerable.BaseValue;
        decimal doom = DynamicVars["CollectorDoomPower"].BaseValue;
        foreach (var enemy in CombatState.HittableEnemies.Where(e => e.IsAlive).ToList())
        {
            if (CombatManager.Instance.IsOverOrEnding)
                break;
            if (!enemy.IsAlive)
                continue;

            await PowerCmd.Apply<VulnerablePower>(choiceContext, enemy, vuln, Owner.Creature, this);
            if (enemy.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                await CollectorMechanics.ApplyDoomAsync(choiceContext, enemy, doom, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Vulnerable.UpgradeValueBy(1m);
        DynamicVars["CollectorDoomPower"].UpgradeValueBy(2m);
    }
}
