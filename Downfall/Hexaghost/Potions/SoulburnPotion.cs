using Downfall.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hexaghost.Potions;

/// <summary>
/// Molotov (SoulburnPotion): Apply 40 Soulburn to target enemy.
/// </summary>
public sealed class SoulburnPotion : HexaPotion
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<SoulburnPower>(40m)
    ];

    public override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HexaCard.SoulburnHoverTip
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (target is null || !target.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await HexaMechanics.ApplySoulburnAsync(
            choiceContext,
            target,
            DynamicVars[nameof(SoulburnPower)].IntValue,
            Owner.Creature);
    }
}
