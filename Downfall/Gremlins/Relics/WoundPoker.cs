using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Gremlins.Relics;

public sealed class WoundPoker : GremlinRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Unpowered)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>()
    ];

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Creature.Side
            || !participants.Contains(Owner.Creature)
            || Owner.Creature.CombatState is not { } combatState
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var weakened = combatState.HittableEnemies
            .Where(e => e.IsAlive && e.HasPower<WeakPower>())
            .ToList();
        if (weakened.Count == 0)
            return;

        Flash();
        await CreatureCmd.Damage(
            choiceContext,
            weakened,
            DynamicVars.Damage.BaseValue,
            ValueProp.Unpowered,
            dealer: Owner.Creature,
            cardSource: null,
            cardPlay: null);
    }
}
