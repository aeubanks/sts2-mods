using Downfall.Awakened.Powers;
using Downfall.Hermit.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Awakened.Cards;

public sealed class DeathCoil()
    : AwakenedCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<ManaburnPower>(8m),
        new EnergyVar(1)
    ];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<ManaburnPower>(),
        HoverTipFactory.FromPower<HermitDrainedPower>(),
        HoverTipFactory.ForEnergy(this)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target is null)
            return;

        await PowerCmd.Apply<ManaburnPower>(
            choiceContext,
            cardPlay.Target,
            DynamicVars[nameof(ManaburnPower)].BaseValue,
            Owner.Creature,
            this);
        await PowerCmd.Apply<HermitDrainedPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars.Energy.BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(ManaburnPower)].UpgradeValueBy(3m);
    }
}
