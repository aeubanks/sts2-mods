using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Slimebound.Potions;

public sealed class ThreeZeroPotion : SlimePotion
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(4)
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (Owner.Creature.CombatState is not { } combatState)
            return;

        var zeroCostPool = Owner.Character.CardPool
            .GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
            .Where(c => c.CanBeGeneratedInCombat
                && c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare
                && c.EnergyCost.Canonical == 0
                && !c.EnergyCost.CostsX)
            .ToList();

        if (zeroCostPool.Count == 0)
            return;

        int count = DynamicVars.Cards.IntValue;
        var rng = Owner.RunState.Rng.CombatCardGeneration;
        for (int i = 0; i < count; i++)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            var template = zeroCostPool[rng.NextInt(zeroCostPool.Count)];
            var card = combatState.CreateCard(template, Owner);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
        }
    }
}
