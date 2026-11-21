using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Automaton.Potions;

/// <summary>
/// Kio's Clever Concoction (BuildAFunctionPotion, Rare):
/// Choose 1 of 3 random cards to Encode; repeat until you create a Function.
/// If potency > 1, gain additional copies of the compiled Function.
/// </summary>
public sealed class BuildAFunctionPotion : BronzePotion
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Copies", 1m)
    ];

    public override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BronzeCard.EncodeHoverTip,
        BronzeCard.FunctionHoverTip
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (Owner.Creature.CombatState is not { } combatState
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var state = AutomatonCombatState.Get(combatState);
        int extraCopies = Math.Max(0, DynamicVars["Copies"].IntValue - 1);
        state.ExtraFunctionCopiesQueued += extraCopies;

        bool compiled = false;
        try
        {
            compiled = await AutomatonMechanics.BuildFunctionFromChoicesAsync(choiceContext, Owner);
        }
        finally
        {
            if (!compiled && extraCopies > 0)
            {
                state.ExtraFunctionCopiesQueued = Math.Max(0, state.ExtraFunctionCopiesQueued - extraCopies);
            }
        }
    }
}
