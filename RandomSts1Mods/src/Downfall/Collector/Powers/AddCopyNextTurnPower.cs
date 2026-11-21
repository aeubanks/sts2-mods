using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Powers;

/// <summary>
/// Gaining Card (AddCopyNextTurnPower): At the start of your next turn, add stored card copy/copies into your hand.
/// Supports multiple queued cards cleanly on a single PowerModel instance.
/// </summary>
public sealed class AddCopyNextTurnPower : CollectorPower
{
    private List<CardModel> _queuedCards = [];

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new StringVar("CardNames", "a card")
    ];

    public void QueueCard(CardModel card)
    {
        AssertMutable();
        _queuedCards.Add(card.CreateClone());
        UpdateCardNamesVar();
    }

    private void UpdateCardNamesVar()
    {
        if (DynamicVars["CardNames"] is StringVar sv)
        {
            sv.StringValue = _queuedCards.Count > 0
                ? string.Join(", ", _queuedCards.Select(c => c.Title))
                : "a card";
        }
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _queuedCards = [.. _queuedCards.Select(c => c.CreateClone())];
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var toAdd = _queuedCards.ToList();
        _queuedCards.Clear();
        Flash();
        await PowerCmd.Remove(this);

        if (CombatState is null || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        foreach (var copy in toAdd)
        {
            if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, player);
        }
    }
}
