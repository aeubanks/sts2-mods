using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hexaghost.Relics;

/// <summary>
/// Soul of Chaos (SoulOfChaos): At the start of your turn, move to a random Ghostflame
/// and randomize it. At the end of your turn, play the top card of your draw pile.
/// </summary>
public sealed class SoulOfChaos : HexaRelic
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HexaCard.GhostflameHoverTip
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        await HexaMechanics.EnsureWheelInitializedAsync(choiceContext, Owner);
        if (HexaMechanics.GetGhostflames(Owner).Count == 0
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await HexaMechanics.SwitchToRandomOtherGhostflameAsync(choiceContext, Owner);
        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            HexaMechanics.RandomizeActiveGhostflame(Owner);
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player
            || !participants.Contains(Owner.Creature)
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await CardPileCmd.AutoPlayFromDrawPile(choiceContext, Owner, 1, CardPilePosition.Top, forceExhaust: false);
    }
}
