using Downfall.Hexaghost.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Here and Now (GhostOfSpirePresent): At the start of your turn, gain 1 Energy.
/// At the end of your turn, Extinguish the active Ghostflame instead of Advancing.
/// Cost: 2 (1).
/// </summary>
public sealed class GhostOfSpirePresent() : HexaCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EnergyHoverTip,
        ExtinguishHoverTip,
        GhostflameHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<HexaEnergyPerTurnPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await PowerCmd.Apply<StopFromAdvancingPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
