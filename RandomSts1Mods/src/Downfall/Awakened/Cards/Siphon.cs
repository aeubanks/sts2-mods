using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Awakened.Cards;

public sealed class Siphon()
    : AwakenedCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override bool UsesChant => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(9m, ValueProp.Move),
        new PowerVar<StrengthPower>(2m)
    ];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target is null)
            return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        if (!cardPlay.Target.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        int chantTriggers = TryTriggerChantCount(cardPlay);
        if (chantTriggers <= 0)
            return;

        decimal amount = DynamicVars.Strength.BaseValue;
        for (int i = 0; i < chantTriggers; i++)
        {
            if (!cardPlay.Target.IsAlive || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            int targetStrBefore = cardPlay.Target.GetPowerAmount<StrengthPower>();
            await PowerCmd.Apply<PiercingWailPower>(
                choiceContext,
                cardPlay.Target,
                amount,
                Owner.Creature,
                this);

            int stolen = targetStrBefore - cardPlay.Target.GetPowerAmount<StrengthPower>();
            if (stolen > 0 && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            {
                await PowerCmd.Apply<FlexPotionPower>(
                    choiceContext,
                    Owner.Creature,
                    stolen,
                    Owner.Creature,
                    this);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars.Strength.UpgradeValueBy(1m);
    }
}
