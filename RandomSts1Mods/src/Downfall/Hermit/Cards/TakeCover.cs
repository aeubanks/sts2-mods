using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hermit.Cards;

public sealed class TakeCover()
    : HermitCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "take_cover";

    protected override bool HasEnergyCostX => true;

    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<HermitDefend>(upgrade: IsUpgraded)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        int upgrades = ResolveEnergyXValue() + (IsUpgraded ? 1 : 0);

        var defend = CombatState.CreateCard<HermitDefend>(Owner);
        defend.EnergyCost.SetThisCombat(0);
        if (upgrades >= 1)
        {
            CardCmd.Upgrade(defend);
            if (upgrades > 1)
            {
                defend.DynamicVars.Block.BaseValue += 3m * (upgrades - 1);
            }
        }

        await CardPileCmd.AddGeneratedCardToCombat(defend, PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
    }
}
