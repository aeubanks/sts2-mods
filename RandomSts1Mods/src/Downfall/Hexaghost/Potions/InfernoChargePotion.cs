using Downfall.Hexaghost.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hexaghost.Potions;

/// <summary>
/// Inferno Potion (InfernoChargePotion): Your Ghostflame effects affect ALL enemies this combat
/// (stacking repeat count with Sacred Bark / multiple uses).
/// </summary>
public sealed class InfernoChargePotion : HexaPotion
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<FlameAffectAllEnemiesPower>(1m)
    ];

    public override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HexaCard.IgniteHoverTip,
        HexaCard.GhostflameHoverTip
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await PowerCmd.Apply<FlameAffectAllEnemiesPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(FlameAffectAllEnemiesPower)].BaseValue,
            Owner.Creature,
            null);
    }
}
