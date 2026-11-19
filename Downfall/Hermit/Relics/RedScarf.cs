using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hermit.Relics;

public sealed class RedScarf : HermitRelic
{
    private HashSet<(Creature Target, ModelId PowerId)>? _pendingNewDebuffs;
    private HashSet<(Creature Target, ModelId PowerId)> PendingNewDebuffs => _pendingNewDebuffs ??= [];

    protected override string IconKey => "red_scarf";

    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(3m, ValueProp.Unpowered)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _pendingNewDebuffs = null;
    }

    public override Task BeforeCombatStart()
    {
        _pendingNewDebuffs?.Clear();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _pendingNewDebuffs?.Clear();
        return Task.CompletedTask;
    }

    public override Task BeforePowerAmountChanged(
        PowerModel power,
        decimal amount,
        Creature target,
        Creature? applier,
        CardModel? cardSource)
    {
        if (applier == Owner.Creature
            && target != Owner.Creature
            && target.Side != Owner.Creature.Side
            && ((power.Type == PowerType.Debuff && amount > 0m)
                || (power.Type == PowerType.Buff && power.AllowNegative && amount < 0m))
            && !target.HasPower(power.Id)
            && !target.HasPower<ArtifactPower>())
        {
            PendingNewDebuffs.Add((target, power.Id));
        }

        return Task.CompletedTask;
    }

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (applier != Owner.Creature || _pendingNewDebuffs is null || !_pendingNewDebuffs.Remove((power.Owner, power.Id)))
            return;

        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        if (power.Owner.HasPower(power.Id))
        {
            Flash();
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.BaseValue, ValueProp.Unpowered, null);
        }
    }
}
