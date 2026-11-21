using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using Packmaster.Cards.Farmer;

namespace Packmaster.Powers;

public sealed class OutToMarketPower : PackmasterPower
{
    private readonly Dictionary<CardPlay, int> _armedDamage = new();

    protected override string IconKey => "out_to_market_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (Owner.Player is null
            || cardPlay.Card.Owner != Owner.Player
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
            return Task.CompletedTask;

        var previousType = FarmerHelper.GetPreviousCardTypePlayedThisCombat(Owner.Player, cardPlay);
        if (previousType is not null && previousType.Value != cardPlay.Card.Type && Amount > 0)
        {
            _armedDamage[cardPlay] = Amount;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!_armedDamage.Remove(cardPlay, out int damage))
            return;

        if (!Owner.IsAlive
            || Owner.CombatState is not { } combatState
            || CombatManager.Instance.IsOverOrEnding)
            return;

        Flash();
        await CreatureCmd.Damage(
            choiceContext,
            combatState.HittableEnemies,
            damage,
            ValueProp.Unpowered,
            dealer: Owner,
            cardSource: null,
            cardPlay: null);
    }
}
