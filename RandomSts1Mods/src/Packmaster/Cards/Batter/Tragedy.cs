using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Cards.Batter;

public sealed class Tragedy()
    : PackmasterCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    protected override string PortraitKey => "tragedy";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(7m, ValueProp.Move)
    ];

    private bool IsCrit(CardPlay? cardPlay)
    {
        if (CombatState is null)
            return false;

        int handCount = PileType.Hand.GetPile(Owner).Cards.Count;
        bool isPlaying = cardPlay is not null || Pile?.Type == PileType.Play;
        return handCount >= (isPlaying ? 5 : 6);
    }

    protected override bool ShouldGlowGoldInternal => IsCrit(null);

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        return cardSource == this && props.IsPoweredAttack() && IsCrit(cardPlay) ? 1.5m : 1m;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
