using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Retract. Apply 12 (17) Soulburn. Return all Ethereal cards from your Discard Pile to your Hand.
/// </summary>
public sealed class LingeringShades() : HexaCard(1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Soulburn", 12m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        RetractHoverTip,
        SoulburnHoverTip,
        HoverTipFactory.FromKeyword(CardKeyword.Ethereal)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await HexaMechanics.RetractAsync(choiceContext, Owner);

        if (cardPlay.Target.IsAlive && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await ApplySoulburnAsync(choiceContext, cardPlay.Target, DynamicVars["Soulburn"].IntValue);

        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var hand = PileType.Hand.GetPile(Owner);
        var ethereals = PileType.Discard.GetPile(Owner).Cards
            .Where(c => c.Keywords.Contains(CardKeyword.Ethereal))
            .ToList();

        foreach (var card in ethereals)
        {
            if (hand.Cards.Count >= CardPile.MaxCardsInHand || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await CardPileCmd.Add(card, PileType.Hand);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Soulburn"].UpgradeValueBy(5m);
    }
}
