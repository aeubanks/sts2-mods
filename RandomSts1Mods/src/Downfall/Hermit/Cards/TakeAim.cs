using Downfall.Hermit.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hermit.Cards;

public sealed class TakeAim()
    : HermitCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "take_aim";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ConcentratedPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!Owner.Creature.HasPower<TakeAimPower>())
        {
            await PowerCmd.Apply<TakeAimPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }

        if (IsUpgraded && !Owner.Creature.HasPower<ConcentratedPower>())
        {
            await PowerCmd.Apply<ConcentratedPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
    }
}
