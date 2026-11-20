using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Copy Paste (Overload, Uncommon, 1 cost, Exhaust; upgraded removes Exhaust):
/// Play a copy of each Encoded card. They do not Encode.
/// </summary>
public sealed class Overload() : BronzeCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip
    ];

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var encodedCopies = AutomatonMechanics.GetEncodedCards(owner)
            .Select(c => (BronzeCard)c.CreateClone())
            .ToList();

        foreach (var copy in encodedCopies)
        {
            if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            await copy.ExecuteEffectAsync(choiceContext, owner, target: null, cardPlay: cardPlay, sourceCard: copy, isFromPower: false);
        }
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
