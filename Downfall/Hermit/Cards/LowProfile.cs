using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hermit.Cards;

public sealed class LowProfile()
    : HermitCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override string PortraitKey => "low_profile";

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(7m),
        new CalculationExtraVar(4m),
        new CalculatedBlockVar(ValueProp.Move).WithMultiplier((card, _) =>
            card.Owner?.Creature.Powers.Count(p => p.TypeForCurrentAmount == PowerType.Debuff) ?? 0)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.CalculatedBlock.Calculate(null), ValueProp.Move, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.CalculationBase.UpgradeValueBy(2m);
        DynamicVars.CalculationExtra.UpgradeValueBy(1m);
    }
}
