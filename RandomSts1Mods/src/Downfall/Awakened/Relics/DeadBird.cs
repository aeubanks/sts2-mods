using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Awakened.Relics;

public sealed class DeadBird : AwakenedRelic
{
    private const decimal BaseDamage = 4m;

    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(BaseDamage, ValueProp.Unpowered)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Creature.Side
            || !participants.Contains(Owner.Creature)
            || !Owner.Creature.IsAlive
            || Owner.Creature.CombatState is not { } combatState
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var target = combatState.HittableEnemies
            .Where(e => e.IsAlive)
            .OrderBy(e => e.CurrentHp)
            .FirstOrDefault();

        if (target is null)
            return;

        decimal baseDamage = DynamicVars.Damage.BaseValue;
        decimal damage = Math.Max(baseDamage, baseDamage + Owner.Creature.GetPowerAmount<StrengthPower>());
        Flash();
        await CreatureCmd.Damage(
            choiceContext,
            target,
            damage,
            ValueProp.Unpowered,
            dealer: Owner.Creature,
            cardSource: null,
            cardPlay: null);
    }
}
