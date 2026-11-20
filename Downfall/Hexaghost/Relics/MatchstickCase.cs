using Downfall.Hexaghost.Orbs;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Hexaghost.Relics;

/// <summary>
/// Sneaky Teakwood Match (MatchstickCase): The first time you Ignite each turn,
/// you can choose whether to Advance or Retract.
/// </summary>
public sealed class MatchstickCase : HexaRelic, IGhostflameIgniteListener
{
    private bool _triggeredThisTurn;
    private int _cardPlayDepth;
    private bool _pendingChoice;

    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HexaCard.IgniteHoverTip,
        HexaCard.AdvanceHoverTip,
        HexaCard.RetractHoverTip
    ];

    public override Task BeforeCombatStart()
    {
        _triggeredThisTurn = false;
        _cardPlayDepth = 0;
        _pendingChoice = false;
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Creature.Side && participants.Contains(Owner.Creature))
        {
            _triggeredThisTurn = false;
            _pendingChoice = false;
        }
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _triggeredThisTurn = false;
        _cardPlayDepth = 0;
        _pendingChoice = false;
        return Task.CompletedTask;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner)
            _cardPlayDepth++;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner)
            return;

        _cardPlayDepth = Math.Max(0, _cardPlayDepth - 1);
        if (_cardPlayDepth == 0 && _pendingChoice)
        {
            _pendingChoice = false;
            if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                await HexaMechanics.ChooseMoveGhostflameAsync(choiceContext, Owner);
        }
    }

    public async Task OnGhostflameIgnitedAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        GhostflameOrb ghostflame,
        int ignitedCount)
    {
        if (player != Owner
            || _triggeredThisTurn
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _triggeredThisTurn = true;
        Flash();

        // Defer until the current card play resolves (matches DownfallSTS addToBot ordering so the played card finishes first).
        if (_cardPlayDepth > 0)
        {
            _pendingChoice = true;
            return;
        }

        await HexaMechanics.ChooseMoveGhostflameAsync(choiceContext, Owner);
    }
}
