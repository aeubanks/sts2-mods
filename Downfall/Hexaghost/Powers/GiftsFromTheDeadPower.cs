using Downfall.Hexaghost.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Hexaghost.Powers;

/// <summary>
/// Gifts from Beyond (GiftsFromTheDeadPower): At the start of your turn, add Amount random
/// Afterlife card(s) to your Hand.
/// </summary>
public sealed class GiftsFromTheDeadPower : HexaPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HexaCard.AfterlifeHoverTip
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player
            || !Owner.IsAlive
            || Amount <= 0
            || Owner.CombatState is not { } combatState
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var candidates = ModelDb.CardPool<HexaCardPool>()
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .Where(c => c is HexaCard { HasAfterlife: true }
                && c.CanBeGeneratedInCombat
                && c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
            .ToList();

        Flash();
        for (int i = 0; i < Amount; i++)
        {
            if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            CardModel canonical = candidates.Count > 0
                ? player.RunState.Rng.CombatCardGeneration.NextItem(candidates)!
                : ModelDb.Card<Sear>();

            var generated = combatState.CreateCard(canonical, player);
            await CardPileCmd.AddGeneratedCardToCombat(generated, PileType.Hand, player);
        }
    }
}
