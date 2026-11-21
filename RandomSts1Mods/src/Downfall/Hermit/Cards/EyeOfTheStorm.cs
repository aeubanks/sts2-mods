using Downfall.Hermit.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hermit.Cards;

public sealed class EyeOfTheStorm()
    : HermitCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "eye_of_the_storm";

    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ConcentratedPower>(),
        DeadOnHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ConcentratedPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);

        if (Owner.PlayerCombatState is { } pcs)
        {
            int missing = Owner.MaxEnergy - pcs.Energy;
            if (missing > 0)
            {
                await PlayerCmd.GainEnergy(missing, Owner);
            }
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
