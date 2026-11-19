using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Cards.Distortion;

public sealed class Shatter()
    : PackmasterCard(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "shatter";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move)
    ];

    protected override bool ShouldGlowGoldInternal =>
        CombatState is not null
        && CombatState.HittableEnemies.Any(e => CountHpLossesThisTurn(e) > 0);

    private int CountHpLossesThisTurn(Creature target)
    {
        if (CombatState is null)
            return 0;

        int count = 0;
        foreach (var entry in CombatManager.Instance.History.Entries.OfType<DamageReceivedEntry>())
        {
            if (entry.HappenedThisTurn(CombatState)
                && entry.Receiver == target
                && entry.Result.UnblockedDamage > 0)
            {
                count++;
            }
        }
        return count;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int hits = CountHpLossesThisTurn(cardPlay.Target);
        if (hits <= 0)
            return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(hits)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
