using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Awakened.Cards;

public sealed class FlareShot()
    : AwakenedCard(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "flare_shot";

    public override bool UsesChant => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new CardsVar(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        int chantTriggers = TryTriggerChantCount(cardPlay);
        if (chantTriggers > 0)
        {
            await PowerCmd.Apply<DrawCardsNextTurnPower>(
                choiceContext,
                Owner.Creature,
                DynamicVars.Cards.IntValue * chantTriggers,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
