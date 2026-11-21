using Downfall.Automaton.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Automaton.Potions;

/// <summary>
/// Alchyrical Elixir (CleanCodePotion, Uncommon):
/// Remove all Compile Error effects from the next Function you create, and it costs 0 until played.
/// </summary>
public sealed class CleanCodePotion : BronzePotion
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Functions", 1m)
    ];

    public override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BronzeCard.CompileHoverTip,
        BronzeCard.FunctionHoverTip,
        HoverTipFactory.FromPower<CleanCodePower>(),
        HoverTipFactory.FromPower<FreeFunctionPower>()
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        decimal count = DynamicVars["Functions"].BaseValue;
        await PowerCmd.Apply<CleanCodePower>(choiceContext, Owner.Creature, count, Owner.Creature, null);

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await PowerCmd.Apply<FreeFunctionPower>(choiceContext, Owner.Creature, count, Owner.Creature, null);
    }
}
