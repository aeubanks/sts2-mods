using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Decompile (Token from Dev Tools, 0 cost, Exhaust; upgraded gains Retain):
/// Exhaust all Encoded cards. Gain 1 Energy and draw 1 card for each card Exhausted.
/// </summary>
public sealed class Decompile() : BronzeTokenCard(0, CardType.Skill, CardRarity.Token, TargetType.Self)
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

        var orbs = AutomatonMechanics.GetEncodedOrbs(owner).ToList();
        foreach (var orb in orbs)
        {
            if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            var card = orb.EncodedCard;
            AutomatonMechanics.RemoveEncodedOrb(owner, orb);
            if (card is null)
                continue;

            card.SequenceSlotIndex = -1;
            await CardCmd.Exhaust(choiceContext, card);

            if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await PlayerCmd.GainEnergy(1m, owner);

            if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await CardPileCmd.Draw(choiceContext, 1m, owner);
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
