using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Packmaster.Powers;

namespace Packmaster.Cards.EvenOdd;

public sealed class GammaWard()
    : PackmasterCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "gamma_ward";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(4m, ValueProp.Move),
        new PowerVar<GammaWardPower>(12m)
    ];

    protected override bool ShouldGlowGoldInternal => EvenOddHelper.IsNextPlayEven(Owner, CombatState);

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        bool isEven = EvenOddHelper.IsCurrentPlayEven(Owner, CombatState);
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        if (isEven)
        {
            await PowerCmd.Apply<GammaWardPower>(
                choiceContext,
                Owner.Creature,
                DynamicVars["GammaWardPower"].BaseValue,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars["GammaWardPower"].UpgradeValueBy(4m);
    }
}
