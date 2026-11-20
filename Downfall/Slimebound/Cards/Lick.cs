using BaseLib.Utils;
using Downfall.Slimebound.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace Downfall.Slimebound.Cards;

[Pool(typeof(TokenCardPool))]
public sealed class Lick() : SlimeCard(0, CardType.Skill, CardRarity.Token, TargetType.AnyEnemy, showInCardLibrary: false)
{
    public override bool IsLick => true;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<SlimedPower>(4m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GoopHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await SlimeMechanics.ApplyGoopAsync(
            choiceContext,
            cardPlay.Target,
            Owner,
            DynamicVars["SlimedPower"].IntValue,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["SlimedPower"].UpgradeValueBy(2m);
    }
}
