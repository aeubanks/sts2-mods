using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace Downfall.Guardian.Cards;

[Pool(typeof(TokenCardPool))]
public sealed class GearUp() : GuardianCard(1, CardType.Skill, CardRarity.Token, TargetType.Self, showInCardLibrary: false)
{
    public override bool CanBeGeneratedInCombat => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain,
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Brace", 10m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [BraceHoverTip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await GuardianMechanics.BraceAsync(choiceContext, Owner, DynamicVars["Brace"].IntValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Brace"].UpgradeValueBy(5m);
    }
}
