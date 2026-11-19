using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using Packmaster.Powers;

namespace Packmaster.Cards.EnergyAndEcho;

public sealed class Slowing()
    : PackmasterCard(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "slowing";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1),
        new CardsVar(2)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this),
        HoverTipFactory.FromCard<Wound>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SlowingPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars.Energy.BaseValue,
            Owner.Creature,
            this);

        if (CombatState is null)
            return;

        int woundCount = DynamicVars.Cards.IntValue;
        var wounds = Enumerable.Range(0, woundCount)
            .Select(_ => CombatState.CreateCard<Wound>(Owner))
            .ToList();
        await CardPileCmd.AddGeneratedCardsToCombat(wounds, PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(-1m);
    }
}
