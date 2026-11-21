using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Awakened.Cards;

public sealed class Caw()
    : AwakenedCard(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override bool UsesChant => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new DynamicVar("DamageGain", 4m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target is null)
            return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        int chantTriggers = TryTriggerChantCount(cardPlay);
        if (chantTriggers > 0 && Owner.PlayerCombatState is { } pcs)
        {
            decimal gain = DynamicVars["DamageGain"].BaseValue * chantTriggers;
            foreach (var caw in pcs.AllCards.OfType<Caw>().Append(this).Distinct(ReferenceEqualityComparer.Instance).Cast<Caw>())
            {
                caw.DynamicVars.Damage.BaseValue += gain;
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m);
        DynamicVars["DamageGain"].UpgradeValueBy(1m);
    }
}
