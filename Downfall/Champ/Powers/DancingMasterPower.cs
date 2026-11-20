using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Downfall.Champ.Powers;

public sealed class DancingMasterPower : ChampPower
{
    private bool _usedThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Side)
        {
            _usedThisTurn = false;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_usedThisTurn
            || cardPlay.Card.Owner.Creature != Owner
            || cardPlay.Card is not ChampCard { IsFinisher: true }
            || Owner.Player is not { } player
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _usedThisTurn = true;
        Flash();
        await PlayerCmd.GainEnergy(Amount, player);
        if (Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CardPileCmd.Draw(choiceContext, Amount, player);
        }
    }
}
