using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Collector.Potions;

/// <summary>
/// Felwater (TempHPPotion, Common):
/// Gain 8 Temp HP.
/// </summary>
public sealed class TempHPPotion : CollectorPotion
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("TempHp", 8m)
    ];

    public override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CollectorCard.TempHpHoverTip
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await CollectorMechanics.GainTempHpAsync(choiceContext, Owner.Creature, DynamicVars["TempHp"].BaseValue, null);
    }
}
