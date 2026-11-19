using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hermit.Cards;

public sealed class FromBeyond()
    : HermitCard(1, CardType.Skill, CardRarity.Rare, TargetType.RandomEnemy)
{
    protected override string PortraitKey => "from_beyond";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HpLossVar(5m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        int hits = PileType.Exhaust.GetPile(Owner).Cards.Count;
        decimal hpLoss = DynamicVars.HpLoss.BaseValue;

        for (int i = 0; i < hits; i++)
        {
            if (CombatState.HittableEnemies.Count == 0)
                break;

            var target = Owner.RunState.Rng.CombatTargets.NextItem(CombatState.HittableEnemies);
            if (target is null)
                break;

            await CreatureCmd.Damage(
                choiceContext,
                target,
                hpLoss,
                ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
                dealer: Owner.Creature,
                cardSource: this,
                cardPlay: cardPlay);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.HpLoss.UpgradeValueBy(2m);
    }
}
