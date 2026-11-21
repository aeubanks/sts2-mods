using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Cards.HighEnergy;

public sealed class SuperNova()
    : PackmasterCard(6, CardType.Skill, CardRarity.Rare, TargetType.AllEnemies)
{
    protected override string PortraitKey => "super_nova";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("HpLoss", 60m),
        new PowerVar<WeakPower>(3m),
        new PowerVar<VulnerablePower>(3m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<VulnerablePower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.Damage(
            choiceContext,
            CombatState!.HittableEnemies,
            DynamicVars["HpLoss"].BaseValue,
            ValueProp.Unblockable | ValueProp.Unpowered,
            dealer: Owner.Creature,
            cardSource: this,
            cardPlay: cardPlay);

        var living = CombatState.HittableEnemies.ToList();
        if (living.Count > 0)
        {
            await PowerCmd.Apply<WeakPower>(
                choiceContext,
                living,
                DynamicVars["WeakPower"].BaseValue,
                Owner.Creature,
                this);
            await PowerCmd.Apply<VulnerablePower>(
                choiceContext,
                living,
                DynamicVars["VulnerablePower"].BaseValue,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["HpLoss"].UpgradeValueBy(11m);
        DynamicVars["WeakPower"].UpgradeValueBy(2m);
        DynamicVars["VulnerablePower"].UpgradeValueBy(2m);
    }
}
