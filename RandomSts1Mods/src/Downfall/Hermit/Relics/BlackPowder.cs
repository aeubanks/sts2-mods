using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hermit.Relics;

public sealed class BlackPowder : HermitRelic
{
    protected override string IconKey => "black_powder";

    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(2m, ValueProp.Unpowered | ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HermitCard.DeadOnHoverTip
    ];

    internal async Task OnDeadOnTriggeredAsync(int triggers)
    {
        var combatState = Owner.Creature.CombatState;
        if (combatState is null || triggers <= 0 || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        decimal damage = DynamicVars.Damage.BaseValue;
        var ctx = new ThrowingPlayerChoiceContext();
        for (int i = 0; i < triggers; i++)
        {
            if (CombatManager.Instance.IsOverOrEnding || combatState.HittableEnemies.Count == 0)
                break;

            Flash();
            foreach (var enemy in combatState.HittableEnemies.ToList())
            {
                if (!enemy.IsAlive)
                    continue;

                await CreatureCmd.Damage(
                    ctx,
                    enemy,
                    damage,
                    ValueProp.Unpowered | ValueProp.Move,
                    dealer: Owner.Creature,
                    cardSource: null,
                    cardPlay: null);
            }
        }
    }
}
