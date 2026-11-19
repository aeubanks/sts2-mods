using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Gremlins.Relics;

public sealed class GremlinBomb : GremlinRelic
{
    private bool _triggeredThisCombat;

    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(30m, ValueProp.Unpowered)
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _triggeredThisCombat = false;
    }

    public override Task BeforeCombatStart()
    {
        _triggeredThisCombat = false;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _triggeredThisCombat = false;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (_triggeredThisCombat
            || target != Owner.Creature
            || result.UnblockedDamage <= 0
            || !target.IsAlive
            || target.CurrentHp * 2 > target.MaxHp
            || target.CombatState is not { } combatState
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _triggeredThisCombat = true;
        Status = RelicStatus.Disabled;
        Flash();

        var enemies = combatState.HittableEnemies.Where(e => e.IsAlive).ToList();
        if (enemies.Count > 0)
        {
            await CreatureCmd.Damage(
                choiceContext,
                enemies,
                DynamicVars.Damage.BaseValue,
                ValueProp.Unpowered,
                dealer: Owner.Creature,
                cardSource: null,
                cardPlay: null);
        }
    }
}
