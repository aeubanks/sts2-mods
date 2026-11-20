using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Guardian.Potions;

public sealed class AcceleratePotion : GuardianPotion
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("AccelerateCount", 2m)
    ];

    public override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GuardianCard.AccelerateHoverTip,
        GuardianCard.StasisHoverTip
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        await GuardianMechanics.AccelerateAllAsync(choiceContext, Owner, DynamicVars["AccelerateCount"].IntValue);
    }
}
