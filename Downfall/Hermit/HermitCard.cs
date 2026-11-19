using BaseLib.Utils;
using Downfall.Core;
using Downfall.Hermit.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Hermit;

[Pool(typeof(HermitCardPool))]
public abstract class HermitCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true)
    : DownfallCard(cost, type, rarity, target, showInCardLibrary)
{
    private bool _deadOnWhenPlayed;

    protected override string CharacterKey => TheHermit.CharKey;

    public virtual bool UsesDeadOn => false;

    protected bool IsDeadOnInHand()
    {
        if (!UsesDeadOn || CombatState is null || Pile?.Type != PileType.Hand)
            return false;

        if (Owner.Creature.HasPower<ConcentratedPower>())
            return true;

        var cards = Pile.Cards;
        int idx = -1;
        for (int i = 0; i < cards.Count; i++)
        {
            if (ReferenceEquals(cards[i], this))
            {
                idx = i;
                break;
            }
        }
        return idx >= 0 && Math.Abs(2 * idx + 1 - cards.Count) <= 1;
    }

    protected bool IsDeadOnNow => Pile?.Type == PileType.Play ? _deadOnWhenPlayed : IsDeadOnInHand();

    protected override bool ShouldGlowGoldInternal => UsesDeadOn && IsDeadOnInHand();

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        UsesDeadOn ? [DeadOnHoverTip] : [];

    internal static IHoverTip DeadOnHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-DEAD_ON_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-DEAD_ON_KEYWORD.description"));

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        if (UsesDeadOn && ReferenceEquals(card, this) && Pile?.Type != PileType.Play && CombatState is not null)
        {
            _deadOnWhenPlayed = Owner.Creature.HasPower<ConcentratedPower>() || IsDeadOnInHand();
        }
        return true;
    }

    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (ReferenceEquals(card, this) && Pile?.Type != PileType.Play && Pile?.Type != PileType.Hand)
        {
            _deadOnWhenPlayed = false;
        }
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card, this) && cardPlay.PlayIndex >= cardPlay.PlayCount - 1)
        {
            _deadOnWhenPlayed = false;
        }
        return Task.CompletedTask;
    }
}
