using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Cards.Warden;

public sealed class Prediction()
    : PackmasterCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "prediction";
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(8m, ValueProp.Move)
    ];

    private bool NoEnemyIntendsToAttack =>
        CombatState is not null
        && CombatState.HittableEnemies.All(e => e.Monster?.IntendsToAttack != true);

    protected override bool ShouldGlowGoldInternal => NoEnemyIntendsToAttack;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is not null && NoEnemyIntendsToAttack)
        {
            decimal modifiedBlock = Hook.ModifyBlock(
                CombatState,
                Owner.Creature,
                DynamicVars.Block.BaseValue,
                DynamicVars.Block.Props,
                this,
                cardPlay,
                out _);
            decimal floored = Math.Floor(Math.Max(0m, modifiedBlock));
            if (floored > 0m)
            {
                await PowerCmd.Apply<BlockNextTurnPower>(choiceContext, Owner.Creature, floored, Owner.Creature, this);
            }
        }
        else
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
