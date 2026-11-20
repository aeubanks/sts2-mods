using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Draw a card. Advance.
/// Upgraded: Draw a card. Choose to Retract, Advance, or do nothing.
/// </summary>
public sealed class Float() : HexaCard(0, CardType.Skill, CardRarity.Basic, TargetType.Self)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        IsUpgraded
            ? [AdvanceHoverTip, RetractHoverTip]
            : [AdvanceHoverTip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, 1m, Owner);

        if (IsUpgraded)
            await HexaMechanics.ChooseMoveGhostflameAsync(choiceContext, Owner);
        else
            await HexaMechanics.AdvanceAsync(choiceContext, Owner, isEndTurn: false);
    }
}
