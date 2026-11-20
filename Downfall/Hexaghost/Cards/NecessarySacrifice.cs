using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Worthy Sacrifice (NecessarySacrifice): Exhaust 2 cards. Transform exhausted Attacks into random Skills
/// and exhausted Skills into random Attacks (Upgraded: Upgrade the new cards). Exhaust.
/// </summary>
public sealed class NecessarySacrifice() : HexaCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.CombatState is not { } combatState
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var eligible = PileType.Hand.GetPile(Owner).Cards
            .Where(c => !ReferenceEquals(c, this))
            .ToList();
        if (eligible.Count == 0)
            return;

        int count = Math.Min(2, eligible.Count);
        List<CardModel> chosen;
        if (eligible.Count <= count)
        {
            chosen = eligible;
        }
        else
        {
            var prefs = new CardSelectorPrefs(SelectionScreenPrompt, count);
            chosen = (await CardSelectCmd.FromHand(choiceContext, Owner, prefs, c => !ReferenceEquals(c, this), this)).ToList();
        }

        var pool = Owner.Character.CardPool
            .GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
            .Where(c => c.CanBeGeneratedInCombat
                && c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
            .ToList();

        var replacements = new List<CardType>();
        foreach (var card in chosen)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            var origType = card.Type;
            await CardCmd.Exhaust(choiceContext, card, causedByEthereal: false);

            if (origType == CardType.Attack)
                replacements.Add(CardType.Skill);
            else if (origType == CardType.Skill)
                replacements.Add(CardType.Attack);
        }

        foreach (var targetType in replacements)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            var candidates = pool.Where(c => c.Type == targetType).ToList();
            CardModel canonical = candidates.Count > 0
                ? Owner.RunState.Rng.CombatCardGeneration.NextItem(candidates)!
                : targetType == CardType.Attack ? ModelDb.Card<HexaStrike>() : ModelDb.Card<HexaDefend>();

            var created = combatState.CreateCard(canonical, Owner);
            if (IsUpgraded && created.IsUpgradable)
                CardCmd.Upgrade(created);
            await CardPileCmd.AddGeneratedCardToCombat(created, PileType.Hand, Owner);
        }
    }
}
