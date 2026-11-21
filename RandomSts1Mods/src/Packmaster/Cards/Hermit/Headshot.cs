using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Cards.Hermit;

public sealed class Headshot()
    : PackmasterCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    private bool _deadOnWhenPlayed;

    protected override string PortraitKey => "headshot";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(7m, ValueProp.Move)
    ];

    private bool IsInMiddleOfHand()
    {
        if (CombatState is null || Pile?.Type != PileType.Hand)
            return false;

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

    protected override bool ShouldGlowGoldInternal => IsInMiddleOfHand();

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        if (ReferenceEquals(card, this) && Pile?.Type != PileType.Play)
        {
            _deadOnWhenPlayed = IsInMiddleOfHand();
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

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (!ReferenceEquals(cardSource, this) || !props.IsPoweredAttack())
            return 1m;

        bool isDeadOn = Pile?.Type == PileType.Play ? _deadOnWhenPlayed : IsInMiddleOfHand();
        return isDeadOn ? 2m : 1m;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card, this) && cardPlay.PlayIndex >= cardPlay.PlayCount - 1)
        {
            _deadOnWhenPlayed = false;
        }
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
