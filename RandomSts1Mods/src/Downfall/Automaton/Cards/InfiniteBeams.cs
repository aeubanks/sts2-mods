using Downfall.Automaton.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Infinite Beams (Uncommon, 1 cost, Power):
/// At the start of each turn, add a Minor Beam (Minor Beam+ when upgraded) to your Hand.
/// </summary>
public sealed class InfiniteBeams() : BronzeCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<MinorBeam>(upgrade: IsUpgraded)
    ];

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        if (IsUpgraded)
        {
            await PowerCmd.Apply<InfiniteBeamsPlusPower>(
                choiceContext,
                owner.Creature,
                1m,
                owner.Creature,
                isFromPower ? null : sourceCard);
        }
        else
        {
            await PowerCmd.Apply<InfiniteBeamsPower>(
                choiceContext,
                owner.Creature,
                1m,
                owner.Creature,
                isFromPower ? null : sourceCard);
        }
    }
}
