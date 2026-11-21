using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Hexaghost.Powers;

/// <summary>
/// Intensity: Increases (or decreases when negative) the damage, Soulburn, and Block
/// applied by Ghostflames when Ignited.
/// </summary>
public sealed class EnhancePower : HexaPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override bool AllowNegative => true;

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power == this && Owner.Player is { } player)
            HexaMechanics.RefreshGhostflameVisuals(player);
        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        if (oldOwner.Player is { } player)
            HexaMechanics.RefreshGhostflameVisuals(player);
        return Task.CompletedTask;
    }
}
