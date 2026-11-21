using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Awakened.Cards;

public sealed class FeatherWhirl()
    : AwakenedCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override bool HasEnergyCostX => true;

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromCard<PlumeJab>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is null)
            return;

        int count = ResolveEnergyXValue() + (IsUpgraded ? 1 : 0);
        for (int i = 0; i < count; i++)
        {
            var jab = CombatState.CreateCard<PlumeJab>(Owner);
            await CardPileCmd.AddGeneratedCardToCombat(jab, PileType.Hand, Owner);
        }
    }

    protected override void OnUpgrade()
    {
    }
}
