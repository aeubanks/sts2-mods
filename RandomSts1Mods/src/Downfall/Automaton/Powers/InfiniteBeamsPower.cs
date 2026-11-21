using Downfall.Automaton.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Automaton.Powers;

/// <summary>
/// Infinite Beams: At the start of each turn, add Amount Minor Beam(s) to your Hand.
/// </summary>
public sealed class InfiniteBeamsPower : BronzePower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<MinorBeam>()
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player
            || Owner.CombatState is not { } combatState
            || Amount <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        for (int i = 0; i < Amount; i++)
        {
            if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            var beam = combatState.CreateCard<MinorBeam>(player);
            await AutomatonMechanics.AddGeneratedCardToHandOrDiscardAsync(beam, player);
        }
    }
}

/// <summary>
/// Infinite Beams+: At the start of each turn, add Amount Minor Beam+(s) to your Hand.
/// </summary>
public sealed class InfiniteBeamsPlusPower : BronzePower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<MinorBeam>(upgrade: true)
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player
            || Owner.CombatState is not { } combatState
            || Amount <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        for (int i = 0; i < Amount; i++)
        {
            if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            var beam = combatState.CreateCard<MinorBeam>(player);
            CardCmd.Upgrade(beam);
            await AutomatonMechanics.AddGeneratedCardToHandOrDiscardAsync(beam, player);
        }
    }
}
