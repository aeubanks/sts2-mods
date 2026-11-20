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
/// Library (Rare, 3 -> 2 cost, Power):
/// At the start of your turn, add a random card with Encode to your Hand. It costs 0 until played.
/// </summary>
public sealed class Library() : BronzeCard(3, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
        HoverTipFactory.FromPower<LibraryPower>()
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

        await PowerCmd.Apply<LibraryPower>(
            choiceContext,
            owner.Creature,
            1m,
            owner.Creature,
            isFromPower ? null : sourceCard);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
