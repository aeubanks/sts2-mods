using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Cultist Strike (ChosenStrike, Rare, 2 -> 1 cost, Strike, Encodes):
/// Deal 6 damage. Compile - Increase this card's damage by 1 permanently.
/// </summary>
public sealed class ChosenStrike() : BronzeCard(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    private int _bonusDamage;

    public override bool Encodes => true;
    public override string FunctionAdjective => "cultist";
    public override string FunctionNoun => "Caw";

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    [SavedProperty]
    public int BonusDamage
    {
        get => _bonusDamage;
        set
        {
            AssertMutable();
            int delta = value - _bonusDamage;
            _bonusDamage = value;
            DynamicVars.Damage.BaseValue += delta;
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        new IntVar("Bonus", 1m)
    ];

    protected override IEnumerable<string> ExtraFineTunableVarNames => ["Bonus"];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
        CompileHoverTip,
        FunctionHoverTip
    ];

    public override string GetFunctionEffectSummary(Player? owner, Creature? target) =>
        $"Deal [blue]{DynamicVars.Damage.IntValue}[/blue] damage.";

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        await DealDamageAsync(choiceContext, owner, target, cardPlay, sourceCard, isFromPower, DynamicVars.Damage.BaseValue);
    }

    public override void OnCompilePreEmbed(Player player, IReadOnlyList<BronzeCard> encodedCards)
    {
        int inc = DynamicVars["Bonus"].IntValue;
        if (inc <= 0)
            return;

        var updated = new HashSet<ChosenStrike>(ReferenceEqualityComparer.Instance);
        void Buff(ChosenStrike card)
        {
            if (updated.Add(card))
                card.BonusDamage += inc;
        }

        Buff(this);

        if (DeckVersion is ChosenStrike deckCopy)
        {
            Buff(deckCopy);

            if (player.PlayerCombatState is { } pcs)
            {
                foreach (var c in pcs.AllCards.OfType<ChosenStrike>())
                {
                    if (ReferenceEquals(c.DeckVersion, deckCopy))
                        Buff(c);
                }
            }

            foreach (var encodedCard in encodedCards.OfType<ChosenStrike>())
            {
                if (ReferenceEquals(encodedCard.DeckVersion, deckCopy))
                    Buff(encodedCard);
            }
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
