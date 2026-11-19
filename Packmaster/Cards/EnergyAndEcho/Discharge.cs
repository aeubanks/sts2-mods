using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Cards.EnergyAndEcho;

public sealed class Discharge()
    : PackmasterCard(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "discharge";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(0m),
        new ExtraDamageVar(5m),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier((card, _) =>
            EchoHelper.GetEnergySpentThisTurn(card.Owner, card.CombatState))
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int priorEnergySpent = Math.Max(0, EchoHelper.GetEnergySpentThisTurn(Owner, CombatState) - cardPlay.Resources.EnergySpent);
        decimal baseDamage = DynamicVars.CalculationBase.BaseValue
            + DynamicVars.ExtraDamage.BaseValue * priorEnergySpent;
        await DamageCmd.Attack(baseDamage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
