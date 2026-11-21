using Downfall.Hermit.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hermit.Cards;

public sealed class Snipe()
    : HermitCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "snipe";

    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        IsUpgraded
            ? [DeadOnHoverTip, HoverTipFactory.FromPower<ConcentratedPower>()]
            : [DeadOnHoverTip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SnipePower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);

        if (IsUpgraded && !Owner.Creature.HasPower<ConcentratedPower>())
        {
            await PowerCmd.Apply<ConcentratedPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
    }
}
