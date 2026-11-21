using Downfall.Hexaghost.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Infernal Form (HexaLevel): At the start of your turn, gain 2 Intensity.
/// Upgraded: Also immediately gain 2 Intensity.
/// </summary>
public sealed class HexaLevel() : HexaCard(3, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<HexalevelPower>(2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        IntensityHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal amt = DynamicVars[nameof(HexalevelPower)].BaseValue;
        await PowerCmd.Apply<HexalevelPower>(choiceContext, Owner.Creature, amt, Owner.Creature, this);
        if (IsUpgraded && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await PowerCmd.Apply<EnhancePower>(choiceContext, Owner.Creature, amt, Owner.Creature, this);
    }
}
