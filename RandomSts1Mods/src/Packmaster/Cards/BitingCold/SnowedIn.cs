using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Packmaster.Powers;

namespace Packmaster.Cards.BitingCold;

public sealed class SnowedIn()
    : PackmasterCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "snowed_in";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<FrostbitePower>(11m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<FrostbitePower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await PowerCmd.Apply<FrostbitePower>(
            choiceContext,
            cardPlay.Target,
            DynamicVars["FrostbitePower"].BaseValue,
            Owner.Creature,
            this);
        await PowerCmd.Apply<SnowedInPower>(
            choiceContext,
            cardPlay.Target,
            1m,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["FrostbitePower"].UpgradeValueBy(3m);
    }
}
