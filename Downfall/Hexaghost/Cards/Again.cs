using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Fleeting Flash (Again): Ignite the active Ghostflame, then Extinguish it.
/// Cost: 1 (0).
/// </summary>
public sealed class Again() : HexaCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        IgniteHoverTip,
        ExtinguishHoverTip,
        GhostflameHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await HexaMechanics.IgniteActiveAsync(choiceContext, Owner, extinguishFirstIfIgnited: true);
        await HexaMechanics.ExtinguishActiveAsync(choiceContext, Owner);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
