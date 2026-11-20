using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Deal 3 (6) damage. Draw an Ethereal card from your Draw Pile.
/// </summary>
public sealed class StrikeFromBeyond() : HexaCard(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Ethereal)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (CombatState is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var hand = PileType.Hand.GetPile(Owner);
        if (hand.Cards.Count >= CardPile.MaxCardsInHand
            || !Hook.ShouldDraw(CombatState, Owner, fromHandDraw: false, out _))
        {
            return;
        }

        var drawPile = PileType.Draw.GetPile(Owner);
        var ethereals = drawPile.Cards
            .Where(c => c.Keywords.Contains(CardKeyword.Ethereal))
            .ToList();
        if (ethereals.Count > 0)
        {
            var picked = Owner.RunState.Rng.CombatCardSelection.NextItem(ethereals);
            if (picked is not null)
            {
                drawPile.MoveToTopInternal(picked);
                await CardPileCmd.Draw(choiceContext, 1, Owner);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
