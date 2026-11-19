using MegaCrit.Sts2.Core.Entities.Cards;

namespace Downfall.Awakened;

public abstract class AwakenedSpellCard(
    int cost,
    CardType type,
    TargetType target)
    : AwakenedCard(cost, type, CardRarity.Token, target, showInCardLibrary: false)
{
    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Exhaust];
}
