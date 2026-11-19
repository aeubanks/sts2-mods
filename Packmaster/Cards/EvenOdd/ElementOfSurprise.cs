using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Packmaster.Cards.EvenOdd;

public sealed class ElementOfSurprise()
    : PackmasterCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override string PortraitKey => "element_of_surprise";

    protected override bool ShouldGlowGoldInternal => EvenOddHelper.IsNextPlayFirst(Owner, CombatState);

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (EvenOddHelper.IsCurrentPlayFirst(Owner, CombatState))
        {
            await PowerCmd.Apply<DuplicationPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
