using Downfall.Slimebound.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Slimebound.Cards;

public sealed class DuplicatedForm() : SlimeCard(3, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<DuplicatedFormPower>(1m),
        new EnergyVar(1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        IsUpgraded ? [HoverTipFactory.ForEnergy(this)] : [];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<DuplicatedFormPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["DuplicatedFormPower"].BaseValue,
            Owner.Creature,
            this);

        if (IsUpgraded && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<DuplicatedFormEnergyPower>(
                choiceContext,
                Owner.Creature,
                DynamicVars.Energy.BaseValue,
                Owner.Creature,
                this);
        }
    }
}
