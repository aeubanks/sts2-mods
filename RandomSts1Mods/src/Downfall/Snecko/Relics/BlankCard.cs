using Downfall.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Snecko.Relics;

public sealed class BlankCard : SneckoRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        DownfallEcho.EchoHoverTip
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || Owner.Creature.CombatState is not { } combatState
            || Owner.PlayerCombatState is not { } pcs
            || combatState.RoundNumber != 1
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var candidates = pcs.AllCards
            .Where(c => c.DeckVersion is not null
                && c.Type is CardType.Attack or CardType.Skill or CardType.Power
                && !DownfallEcho.IsEcho(c))
            .ToList();
        if (candidates.Count == 0)
            return;

        var chosen = candidates[Owner.RunState.Rng.CombatCardGeneration.NextInt(candidates.Count)];
        var echo = DownfallEcho.MakeEcho(chosen);
        if (echo is null)
            return;

        if (!echo.EnergyCost.CostsX && echo.EnergyCost.Canonical >= 0)
        {
            echo.EnergyCost.SetThisTurnOrUntilPlayed(0, reduceOnly: true);
        }

        Flash();
        await CardPileCmd.AddGeneratedCardToCombat(echo, PileType.Hand, player);
    }
}
