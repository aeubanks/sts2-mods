using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Awakened.Relics;

public sealed class ShardOfNowak : AwakenedRelic
{
    private const decimal StrengthBonus = 3m;

    private bool _bonusActive;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<StrengthPower>(StrengthBonus)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _bonusActive = false;
    }

    public override Task BeforeCombatStart()
    {
        _bonusActive = false;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Creature.Side
            && participants.Contains(Owner.Creature)
            && combatState.RoundNumber == 1
            && !_bonusActive
            && Owner.Creature.IsAlive
            && !CombatManager.Instance.IsOverOrEnding)
        {
            _bonusActive = true;
            Status = RelicStatus.Normal;
            Flash();
            await PowerCmd.Apply<StrengthPower>(
                choiceContext,
                Owner.Creature,
                DynamicVars.Strength.BaseValue,
                Owner.Creature,
                null);
        }
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (!_bonusActive
            || target != Owner.Creature
            || result.UnblockedDamage <= 0
            || !CombatManager.Instance.IsInProgress
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _bonusActive = false;
        Status = RelicStatus.Disabled;
        Flash();
        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            Owner.Creature,
            -DynamicVars.Strength.BaseValue,
            Owner.Creature,
            null);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _bonusActive = false;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }
}
