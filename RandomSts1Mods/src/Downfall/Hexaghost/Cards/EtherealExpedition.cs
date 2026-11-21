using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Eerie Expedition (EtherealExpedition): Add a random Ethereal card into your Hand.
/// It costs 0 this turn. Exhaust. Cost: 1 (0).
/// </summary>
public sealed class EtherealExpedition() : HexaCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Ethereal)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.CombatState is not { } combatState)
            return;

        var candidates = Owner.Character.CardPool
            .GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
            .Where(c => c.CanBeGeneratedInCombat
                && c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare
                && c.CanonicalKeywords.Contains(CardKeyword.Ethereal))
            .ToList();

        CardModel chosenCanonical = candidates.Count > 0
            ? Owner.RunState.Rng.CombatCardGeneration.NextItem(candidates)!
            : ModelDb.Card<Sear>();

        var generated = combatState.CreateCard(chosenCanonical, Owner);
        generated.EnergyCost.SetThisTurn(0, reduceOnly: true);
        await CardPileCmd.AddGeneratedCardToCombat(generated, PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
