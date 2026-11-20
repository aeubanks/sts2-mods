using Downfall.Hexaghost.Orbs;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hexaghost.Relics;

public abstract class SpiritBrandBase : HexaRelic, IGhostflameIgniteListener
{
    public const int BlockAmount = 4;

    public override RelicRarity Rarity => RelicRarity.Starter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HexaCard.IgniteHoverTip,
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != Owner.Creature.Side
            || !participants.Contains(Owner.Creature)
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        if (combatState.RoundNumber == 1)
            await HexaMechanics.EnsureWheelInitializedAsync(choiceContext, Owner);

        OnPlayerTurnStartReset();
    }

    protected virtual void OnPlayerTurnStartReset() { }

    public abstract Task OnGhostflameIgnitedAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        GhostflameOrb ghostflame);
}

/// <summary>
/// Starter relic: The first time you Ignite a Ghostflame each turn, gain 4 Block.
/// Upgrades via Touch of Orobas into Mark of the Ether (UnbrokenSoul).
/// </summary>
public sealed class SpiritBrand : SpiritBrandBase
{
    private bool _triggeredThisTurn;

    public override RelicModel? GetUpgradeReplacement() => ModelDb.Relic<UnbrokenSoul>();

    protected override void OnPlayerTurnStartReset()
    {
        _triggeredThisTurn = false;
    }

    public override Task AfterCombatEnd(MegaCrit.Sts2.Core.Rooms.CombatRoom room)
    {
        _triggeredThisTurn = false;
        return Task.CompletedTask;
    }

    public override async Task OnGhostflameIgnitedAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        GhostflameOrb ghostflame)
    {
        if (player != Owner || _triggeredThisTurn || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        _triggeredThisTurn = true;
        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, BlockAmount, ValueProp.Unpowered, null);
    }
}

/// <summary>
/// Orobas upgrade of Spirit Brand ("Mark of the Ether"):
/// Whenever you Ignite a Ghostflame, gain 4 Block.
/// </summary>
public sealed class UnbrokenSoul : SpiritBrandBase
{
    public override async Task OnGhostflameIgnitedAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        GhostflameOrb ghostflame)
    {
        if (player != Owner || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, BlockAmount, ValueProp.Unpowered, null);
    }
}
