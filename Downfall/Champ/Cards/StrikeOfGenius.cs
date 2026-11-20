using Downfall.Champ.Powers;
using Downfall.Core;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Champ.Cards;

public sealed class StrikeOfGenius()
    : ChampCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        DownfallEcho.EchoHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (IsUpgraded)
        {
            await PowerCmd.Apply<StrikeOfGeniusUpgradedPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }
        else
        {
            await PowerCmd.Apply<StrikeOfGeniusPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }
    }
}
