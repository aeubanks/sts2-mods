using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Champ.Cards;

public sealed class FocusedBerserking()
    : ChampCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool IsFinisher => true;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new RepeatVar(2)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        FinisherHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await ChampStances.ExecuteFinisherAsync(
            Owner,
            choiceContext,
            noExit: true,
            bonusTimes: DynamicVars.Repeat.IntValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(1m);
    }
}
