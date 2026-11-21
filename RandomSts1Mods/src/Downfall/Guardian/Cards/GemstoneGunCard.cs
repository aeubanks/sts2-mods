using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace Downfall.Guardian.Cards;

[Pool(typeof(TokenCardPool))]
public sealed class GemstoneGunCard()
    : GuardianCard(0, CardType.Skill, CardRarity.Token, TargetType.Self, showInCardLibrary: false)
{
    public override int MaxSockets => 3;
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedInCombat => false;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GemHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await TriggerSocketedGemsAsync(choiceContext, cardPlay);
    }
}
