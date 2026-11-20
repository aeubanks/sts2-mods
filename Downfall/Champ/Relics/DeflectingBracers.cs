using Downfall.Champ.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Champ.Relics;

public sealed class DeflectingBracers : ChampRelic
{
    private int _blockBeforeClear;

    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<CounterPower>(),
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _blockBeforeClear = 0;
    }

    public override Task BeforeCombatStart()
    {
        _blockBeforeClear = 0;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _blockBeforeClear = 0;
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Creature.Side)
        {
            _blockBeforeClear = Owner.Creature.Block;
        }
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Creature.Side)
        {
            _blockBeforeClear = 0;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterBlockCleared(Creature creature)
    {
        if (creature != Owner.Creature
            || _blockBeforeClear <= 0
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        int expired = Math.Max(0, _blockBeforeClear - creature.Block);
        _blockBeforeClear = 0;

        int counterGain = expired / 2;
        if (counterGain > 0)
        {
            Flash();
            await PowerCmd.Apply<CounterPower>(
                new ThrowingPlayerChoiceContext(),
                Owner.Creature,
                counterGain,
                Owner.Creature,
                null);
        }
    }
}
