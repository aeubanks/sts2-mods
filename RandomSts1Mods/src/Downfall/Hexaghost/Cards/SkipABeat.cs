using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Kindle (SkipABeat): If the current Ghostflame is not Ignited, Ignite it.
/// Upgraded: Extinguish and Ignite the current Ghostflame.
/// </summary>
public sealed class SkipABeat() : HexaCard(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        IsUpgraded
            ? [ExtinguishHoverTip, IgniteHoverTip, GhostflameHoverTip]
            : [IgniteHoverTip, GhostflameHoverTip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        bool wasIgnitedBeforePlay = Owner.Creature.CombatState is { } combatState
            && HexaCombatState.Get(combatState).WasActiveIgnitedBeforeCardPlay;
        await HexaMechanics.IgniteActiveAsync(
            choiceContext,
            Owner,
            extinguishFirstIfIgnited: IsUpgraded || !wasIgnitedBeforePlay);
    }
}
