using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Snecko.Powers;

public sealed class SerpentsNestPower : SneckoPower
{
    private Dictionary<CardPlay, int> _capturedAmount = [];

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _capturedAmount = [];
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature == Owner && cardPlay.Card.Type == CardType.Power && Amount > 0)
        {
            _capturedAmount[cardPlay] = Amount;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!_capturedAmount.Remove(cardPlay, out int damage)
            || damage <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || Owner.CombatState is not { } combatState)
        {
            return;
        }

        var targets = combatState.HittableEnemies.Where(e => e.IsAlive).ToList();
        if (targets.Count == 0)
            return;

        Flash();
        await CreatureCmd.Damage(choiceContext, targets, damage, ValueProp.Unpowered, dealer: Owner);
    }
}
