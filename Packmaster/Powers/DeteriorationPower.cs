using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Powers;

public sealed class DeteriorationPower : PackmasterPower
{
    protected override string IconKey => "deterioration_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner)
            || Owner.Player is not { } player
            || !Owner.IsAlive
            || Amount <= 0
            || CombatManager.Instance.IsOverOrEnding)
            return;

        var discardPile = PileType.Discard.GetPile(player);
        if (discardPile.Cards.Count == 0)
            return;

        var chosen = discardPile.Cards[player.RunState.Rng.CombatCardSelection.NextInt(discardPile.Cards.Count)];
        Flash();
        await CardCmd.Exhaust(choiceContext, chosen);

        if (Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
        }
    }
}
