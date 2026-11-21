using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hexaghost.Potions;

/// <summary>
/// Combustive Fluid (DoubleChargePotion): Ignite the current Ghostflame 3 times.
/// </summary>
public sealed class DoubleChargePotion : HexaPotion
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("IgniteCount", 3m)
    ];

    public override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HexaCard.IgniteHoverTip,
        HexaCard.GhostflameHoverTip
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        int count = DynamicVars["IgniteCount"].IntValue;
        for (int i = 0; i < count; i++)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            await HexaMechanics.IgniteActiveAsync(choiceContext, Owner, extinguishFirstIfIgnited: true);
        }
    }
}
