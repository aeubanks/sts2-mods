using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Collector.Potions;

/// <summary>
/// Embalming Oils (DebuffDoublePotion, Rare):
/// Double (Sacred Bark: Triple) the target's stackable debuffs.
/// </summary>
public sealed class DebuffDoublePotion : CollectorPotion
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("ExtraCopies", 1m)
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        var resolved = CollectorCard.ResolveTarget(Owner, target);
        if (resolved is null || CombatManager.Instance.IsOverOrEnding)
            return;

        int extraCopies = Math.Max(1, DynamicVars["ExtraCopies"].IntValue);
        var debuffs = resolved.Powers
            .Where(p => p.TypeForCurrentAmount == PowerType.Debuff
                        && p.StackType == PowerStackType.Counter
                        && p.Amount != 0)
            .ToList();

        foreach (var debuff in debuffs)
        {
            if (!resolved.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            decimal delta = debuff.Amount * extraCopies;
            if (delta != 0m)
            {
                await PowerCmd.ModifyAmount(choiceContext, debuff, delta, null, null);
            }
        }
    }
}
