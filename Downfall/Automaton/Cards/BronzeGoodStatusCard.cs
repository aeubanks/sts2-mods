using MegaCrit.Sts2.Core.Entities.Cards;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Base class for Bronze Idol's playable "Good Status" replacements.
/// Retains CardType.Status so all Automaton Status synergies (FeaturePower, Repulsor, BurnOut, ShipIt, etc.)
/// continue to recognize them while BronzeIdol skips already-converted Good Status cards.
/// </summary>
public abstract class BronzeGoodStatusCard(int cost, TargetType target)
    : BronzeTokenCard(cost, CardType.Status, CardRarity.Token, target, showInCardLibrary: false)
{
    public override bool CanBeGeneratedInCombat => false;
}
