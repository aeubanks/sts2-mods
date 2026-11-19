using BaseLib.Utils;
using Downfall.Awakened.Powers;
using Downfall.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace Downfall.Awakened;

[Pool(typeof(AwakenedCardPool))]
public abstract class AwakenedCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true)
    : DownfallCard(cost, type, rarity, target, showInCardLibrary)
{
    private bool _chantActivated;

    protected override string CharacterKey => TheAwakenedOne.CharKey;

    public virtual bool UsesChant => false;
    public virtual bool UsesConjure => false;

    internal static IHoverTip ChantHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-CHANT_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-CHANT_KEYWORD.description"));

    internal static IHoverTip ConjureHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-CONJURE_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-CONJURE_KEYWORD.description"));

    protected virtual IEnumerable<IHoverTip> CardHoverTips => [];

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            if (UsesChant)
                yield return ChantHoverTip;
            if (UsesConjure)
                yield return ConjureHoverTip;
            foreach (var tip in CardHoverTips)
                yield return tip;
        }
    }

    protected override bool ShouldGlowGoldInternal => UsesChant && IsChantActiveInHand();

    internal static bool WasLastCardPlayedPower(Player? owner, ICombatState? combatState, CardPlay? currentPlay = null)
    {
        if (owner is null || combatState is null)
            return false;

        CardPlay? previous = null;
        foreach (var entry in CombatManager.Instance.History.CardPlaysStarted)
        {
            if (entry.CardPlay.Card.Owner != owner)
                continue;

            if (currentPlay is not null && ReferenceEquals(entry.CardPlay, currentPlay))
                return previous is not null && previous.Card.Type == CardType.Power;

            previous = entry.CardPlay;
        }

        return previous is not null && previous.Card.Type == CardType.Power;
    }

    protected bool IsChantActiveInHand() =>
        _chantActivated || WasLastCardPlayedPower(Owner, CombatState);

    protected bool TryTriggerChant(CardPlay cardPlay) =>
        TryTriggerChantCount(cardPlay) > 0;

    protected int TryTriggerChantCount(CardPlay cardPlay)
    {
        if (!UsesChant)
            return 0;

        if (_chantActivated || WasLastCardPlayedPower(Owner, CombatState, cardPlay))
        {
            _chantActivated = true;
            var rising = Owner.Creature.GetPower<RisingChantPower>();
            return rising?.TryConsumeDoubleChant() == true ? 2 : 1;
        }

        return 0;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner
            && cardPlay.Card.Type == CardType.Power
            && CombatState is not null)
        {
            await AwakenedSpellbook.TryCheckAwakeningAsync(CombatState, Owner, choiceContext);
        }
        await base.AfterCardPlayed(choiceContext, cardPlay);
    }
}
