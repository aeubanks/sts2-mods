using Downfall.Snecko.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Snecko.Relics;

public sealed class SneckoSoul : SneckoRelic
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    public override RelicModel GetUpgradeReplacement() => ModelDb.Relic<SuperSneckoSoul>();

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<SoulRoll>()
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || Owner.Creature.CombatState is not { RoundNumber: 1 } combatState)
            return;

        Flash();
        var soulRoll = combatState.CreateCard<SoulRoll>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(soulRoll, PileType.Hand, Owner);
    }
}
