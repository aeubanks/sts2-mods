using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using Packmaster.Powers;

namespace Packmaster.Cards.Warlock;

public sealed class MalchezaarsImp()
    : PackmasterCard(3, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override string PortraitKey => "malchezaars_imp";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        HoverTipFactory.FromCard<Imp>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (IsUpgraded)
        {
            await PowerCmd.Apply<MalchezaarsImpUpgradedPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }
        else
        {
            await PowerCmd.Apply<MalchezaarsImpPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }
    }
}
