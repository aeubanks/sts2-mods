using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Packmaster.Powers;

namespace Packmaster.Cards.Distortion;

public sealed class Darken()
    : PackmasterCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "darken";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<DistortionPower>(7m),
        new IntVar("Bonus", 4m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<DistortionPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        decimal amount = DynamicVars[nameof(DistortionPower)].BaseValue;
        bool hasOtherDebuff = cardPlay.Target.Powers.Any(p =>
            p.TypeForCurrentAmount == PowerType.Debuff && p is not DistortionPower);
        if (hasOtherDebuff)
        {
            amount += DynamicVars["Bonus"].BaseValue;
        }

        await PowerCmd.Apply<DistortionPower>(choiceContext, cardPlay.Target, amount, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(DistortionPower)].UpgradeValueBy(2m);
        DynamicVars["Bonus"].UpgradeValueBy(2m);
    }
}
