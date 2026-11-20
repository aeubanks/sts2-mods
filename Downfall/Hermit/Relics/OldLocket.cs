using Downfall.Hermit.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Hermit.Relics;

public sealed class OldLocket : HermitRelic
{
    protected override string IconKey => "old_locket";

    public override RelicRarity Rarity => RelicRarity.Starter;

    public override RelicModel? GetUpgradeReplacement() => ModelDb.Relic<ClaspedLocket>();

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<MementoCard>()
    ];

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player != Owner || combatState.RoundNumber != 1)
            return;

        Flash();
        var memento = combatState.CreateCard<MementoCard>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(memento, PileType.Hand, Owner, CardPilePosition.Top);
    }
}
