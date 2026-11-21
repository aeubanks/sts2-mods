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
/// Cursed Wail (Uncommon, 2 cost, Skill, Exhaust):
/// ALL enemies lose 9 (11) Strength this turn. Afflicted enemies lose 1 (2) Strength.
/// </summary>
public sealed class CursedWail() : CollectorCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override bool ShouldGlowGoldInternal =>
        CombatState is not null && CombatState.HittableEnemies.Any(CollectorMechanics.IsAfflicted);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("TempStrengthLoss", 9m),
        new IntVar("AfflictedStrengthLoss", 1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        AfflictedHoverTip
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (CombatState is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        decimal tempLoss = DynamicVars["TempStrengthLoss"].BaseValue;
        decimal permLoss = DynamicVars["AfflictedStrengthLoss"].BaseValue;
        var enemies = CombatState.HittableEnemies.Where(e => e.IsAlive).ToList();

        foreach (var enemy in enemies)
        {
            if (CombatManager.Instance.IsOverOrEnding)
                break;
            if (!enemy.IsAlive)
                continue;
            await PowerCmd.Apply<DarkShacklesPower>(choiceContext, enemy, tempLoss, Owner.Creature, this);
        }

        foreach (var enemy in enemies)
        {
            if (CombatManager.Instance.IsOverOrEnding)
                break;
            if (!enemy.IsAlive || !CollectorMechanics.IsAfflicted(enemy))
                continue;
            await PowerCmd.Apply<StrengthPower>(choiceContext, enemy, -permLoss, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["TempStrengthLoss"].UpgradeValueBy(2m);
        DynamicVars["AfflictedStrengthLoss"].UpgradeValueBy(1m);
    }
}
