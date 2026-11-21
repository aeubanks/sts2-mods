using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Cards.Collectibles;

/// <summary>
/// Acid Slime (Collectible, 0 cost, Skill, Exhaust): Add 1 (2) random 0-cost card(s) to your Hand.
/// </summary>
public sealed class AcidSlimeCard() : CollectibleCard(0, CardType.Skill, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1)
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (Owner.Creature.CombatState is not { } combatState
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var zeroCostPool = Owner.Character.CardPool
            .GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
            .Where(c => c.CanBeGeneratedInCombat
                && !c.EnergyCost.CostsX
                && c.EnergyCost.Canonical == 0
                && c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
            .Distinct()
            .ToList();

        int count = DynamicVars.Cards.IntValue;
        for (int i = 0; i < count; i++)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            CardModel canonical = zeroCostPool.Count > 0
                ? Owner.RunState.Rng.CombatCardGeneration.NextItem(zeroCostPool)!
                : ModelDb.Card<Ember>();
            var generated = combatState.CreateCard(canonical, Owner);
            await CollectorMechanics.AddGeneratedCardToHandOrDiscardAsync(generated, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
