using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Hexaghost.Relics;

/// <summary>
/// Recycler (RecyclingMachine): The first time a non-Status, non-Curse Ethereal card is Exhausted
/// each combat, add a copy to your Hand that costs 0 until played.
/// </summary>
public sealed class RecyclingMachine : HexaRelic
{
    private bool _triggeredThisCombat;
    private CardModel? _pendingEndTurnCopy;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Ethereal),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    public override Task BeforeCombatStart()
    {
        _triggeredThisCombat = false;
        _pendingEndTurnCopy = null;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _triggeredThisCombat = false;
        _pendingEndTurnCopy = null;
        return Task.CompletedTask;
    }

    public override async Task AfterCardExhausted(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool causedByEthereal)
    {
        if (_triggeredThisCombat
            || card.Owner != Owner
            || !card.Keywords.Contains(CardKeyword.Ethereal)
            || card.Type is CardType.Status or CardType.Curse
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _triggeredThisCombat = true;
        Flash();

        var copy = card.CreateClone();
        if (!copy.EnergyCost.CostsX)
            copy.EnergyCost.SetUntilPlayed(0);

        // During DoTurnEnd Ethereal exhausts, defer until AfterSideTurnEnd so Hand Flush doesn't immediately discard the recycled card.
        if (causedByEthereal)
        {
            _pendingEndTurnCopy = copy;
            return;
        }

        await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, Owner);
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player
            || !participants.Contains(Owner.Creature)
            || _pendingEndTurnCopy is null)
        {
            return;
        }

        var copy = _pendingEndTurnCopy;
        _pendingEndTurnCopy = null;
        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, Owner);
    }
}
