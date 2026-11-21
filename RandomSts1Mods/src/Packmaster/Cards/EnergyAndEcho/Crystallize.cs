using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Cards.EnergyAndEcho;

public sealed class Crystallize()
    : PackmasterCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override string PortraitKey => "crystallize";

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(0m),
        new CalculationExtraVar(3m),
        new CalculatedBlockVar(ValueProp.Move).WithMultiplier((card, _) =>
            card.CombatState is null ? 0m : (card.Owner?.PlayerCombatState?.Energy ?? 0m))
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int availableEnergy = (Owner.PlayerCombatState?.Energy ?? 0) + cardPlay.Resources.EnergySpent;
        decimal baseBlock = DynamicVars.CalculationBase.BaseValue
            + DynamicVars.CalculationExtra.BaseValue * availableEnergy;
        await CreatureCmd.GainBlock(
            Owner.Creature,
            baseBlock,
            DynamicVars.CalculatedBlock.Props,
            cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.CalculationExtra.UpgradeValueBy(1m);
    }
}
