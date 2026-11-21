using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Retain. Add a random Power card to your Hand. It costs 1 less this combat. Exhaust. Cost: 1 (0).
/// </summary>
public sealed class TimeOfNeed() : HexaCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.CombatState is not { } combatState
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var powers = Owner.Character.CardPool
            .GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
            .Where(c => c.CanBeGeneratedInCombat
                && c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare
                && c.Type == CardType.Power)
            .ToList();

        var rarePowers = powers.Where(c => c.Rarity == CardRarity.Rare).ToList();
        var nonRarePowers = powers.Where(c => c.Rarity != CardRarity.Rare).ToList();

        // Exact DownfallSTS weighting: 20% Rare, 80% non-Rare when both exist
        List<CardModel> bucket;
        if (rarePowers.Count > 0 && nonRarePowers.Count > 0)
        {
            bucket = Owner.RunState.Rng.CombatCardGeneration.NextInt(100) >= 80
                ? rarePowers
                : nonRarePowers;
        }
        else
        {
            bucket = powers;
        }

        CardModel canonical = bucket.Count > 0
            ? Owner.RunState.Rng.CombatCardGeneration.NextItem(bucket)!
            : ModelDb.Card<EmpoweredFlame>();

        var generated = combatState.CreateCard(canonical, Owner);
        if (!generated.EnergyCost.CostsX)
            generated.EnergyCost.AddThisCombat(-1, reduceOnly: true);

        await CardPileCmd.AddGeneratedCardToCombat(generated, PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
