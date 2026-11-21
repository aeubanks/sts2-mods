using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Awakened.Relics;

public sealed class MiniBlackHole : AwakenedRelic
{
    private bool _drawNextTurn;

    public override RelicRarity Rarity => RelicRarity.Shop;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1)
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _drawNextTurn = false;
    }

    private bool PlayedPowerThisTurn(ICombatState combatState) =>
        CombatManager.Instance.History.CardPlaysStarted.Any(e =>
            e.HappenedThisTurn(combatState)
            && e.CardPlay.Card.Owner == Owner
            && e.CardPlay.Card.Type == CardType.Power);

    private void ResetCombatState()
    {
        _drawNextTurn = false;
        Status = RelicStatus.Normal;
    }

    public override Task AfterObtained()
    {
        if (CombatManager.Instance.IsInProgress && Owner.Creature.CombatState is { } combatState)
        {
            _drawNextTurn = false;
            Status = PlayedPowerThisTurn(combatState) ? RelicStatus.Disabled : RelicStatus.Normal;
        }
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        ResetCombatState();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetCombatState();
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await base.AfterCardPlayed(choiceContext, cardPlay);

        if (cardPlay.Card.Owner == Owner && cardPlay.Card.Type == CardType.Power)
        {
            Status = RelicStatus.Disabled;
        }
    }

    public override Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == Owner.Creature.Side
            && participants.Contains(Owner.Creature)
            && Owner.Creature.CombatState is { } combatState)
        {
            _drawNextTurn = !PlayedPowerThisTurn(combatState);
        }
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        bool shouldDraw = _drawNextTurn;
        _drawNextTurn = false;
        Status = RelicStatus.Normal;

        if (shouldDraw)
        {
            Flash();
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
        }
    }
}
