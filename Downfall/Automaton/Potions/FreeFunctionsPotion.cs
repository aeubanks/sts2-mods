using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Automaton.Potions;

/// <summary>
/// Machine Oil (FreeFunctionsPotion, Common):
/// Increase all numbers on Encoded cards by 1 except cost.
/// </summary>
public sealed class FreeFunctionsPotion : BronzePotion
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Amount", 1m)
    ];

    public override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BronzeCard.EncodeHoverTip
    ];

    protected override Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return Task.CompletedTask;

        int amount = DynamicVars["Amount"].IntValue;
        if (amount <= 0)
            return Task.CompletedTask;

        foreach (var card in AutomatonMechanics.GetEncodedCards(Owner))
        {
            card.ApplyFineTune(amount);
        }
        AutomatonMechanics.RefreshOrbVisuals(Owner);
        return Task.CompletedTask;
    }
}
