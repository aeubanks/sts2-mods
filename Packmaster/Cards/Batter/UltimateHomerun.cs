using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Cards.Batter;

public sealed class UltimateHomerun()
    : PackmasterCard(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "ultimate_homerun";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(0m),
        new ExtraDamageVar(1m),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier((card, _) => GetHighestUnblockedAttackDamage(card))
    ];

    private static decimal GetHighestUnblockedAttackDamage(CardModel card)
    {
        if (card.CombatState is null || card.Owner is null)
            return 0m;

        int max = 0;
        foreach (var entry in CombatManager.Instance.History.Entries.OfType<DamageReceivedEntry>())
        {
            if (entry.Dealer == card.Owner.Creature
                && entry.Receiver != card.Owner.Creature
                && entry.Result.Props.IsPoweredAttack()
                && entry.Result.UnblockedDamage > max)
            {
                max = entry.Result.UnblockedDamage;
            }
        }
        return max;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.CalculatedDamage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
