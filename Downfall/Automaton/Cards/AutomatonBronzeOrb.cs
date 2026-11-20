using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Bronze Orb (Uncommon, 1 cost, Innate, Exhaust):
/// Deal 8 (12) damage. Encode a random card with Encode in your Draw Pile.
/// </summary>
public sealed class AutomatonBronzeOrb() : BronzeCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Innate, CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
        FunctionHoverTip
    ];

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        await DealDamageAsync(choiceContext, owner, target, cardPlay, sourceCard, isFromPower, DynamicVars.Damage.BaseValue);

        if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var encodables = PileType.Draw.GetPile(owner).Cards
            .OfType<BronzeCard>()
            .Where(c => c.Encodes)
            .ToList();
        if (encodables.Count > 0)
        {
            var chosen = owner.RunState.Rng.CombatCardSelection.NextItem(encodables);
            if (chosen is not null)
                await AutomatonMechanics.EncodeCardAsync(choiceContext, owner, chosen);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
