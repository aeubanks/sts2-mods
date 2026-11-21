using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Ethereal. Afterlife. Apply 10 (13) Soulburn to ALL enemies twice.
/// Afterlife: Apply 10 (13) Soulburn to ALL enemies once.
/// </summary>
public sealed class FlamesFromBeyond() : HexaCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];
    public override bool HasAfterlife => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Soulburn", 10m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        AfterlifeHoverTip,
        SoulburnHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var combatState = Owner.Creature.CombatState;
        if (combatState is null)
            return;

        int burn = DynamicVars["Soulburn"].IntValue;
        foreach (var enemy in combatState.HittableEnemies.Where(e => e.IsAlive).ToList())
        {
            if (CombatManager.Instance.IsOverOrEnding)
                break;
            await ApplySoulburnAsync(choiceContext, enemy, burn);
            if (enemy.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                await ApplySoulburnAsync(choiceContext, enemy, burn);
        }
    }

    protected override async Task OnAfterlifeAsync(PlayerChoiceContext choiceContext, bool causedByEthereal)
    {
        var combatState = Owner.Creature.CombatState;
        if (combatState is null)
            return;

        int burn = DynamicVars["Soulburn"].IntValue;
        foreach (var enemy in combatState.HittableEnemies.Where(e => e.IsAlive).ToList())
        {
            if (CombatManager.Instance.IsOverOrEnding)
                break;
            await ApplySoulburnAsync(choiceContext, enemy, burn);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Soulburn"].UpgradeValueBy(3m);
    }
}
