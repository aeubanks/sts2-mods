using Downfall.Awakened.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Awakened.Relics;

public sealed class HexxBomb : AwakenedRelic
{
    private Dictionary<Creature, int>? _dyingManaburn;
    private Dictionary<Creature, int> DyingManaburn => _dyingManaburn ??= [];

    public override RelicRarity Rarity => RelicRarity.Shop;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ManaburnPower>()
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _dyingManaburn = null;
    }

    public override Task BeforeCombatStart()
    {
        _dyingManaburn?.Clear();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _dyingManaburn?.Clear();
        return Task.CompletedTask;
    }

    public override Task BeforeDeath(Creature creature)
    {
        if (creature.IsEnemy)
        {
            int amount = creature.GetPowerAmount<ManaburnPower>();
            if (amount > 0)
            {
                DyingManaburn[creature] = amount;
            }
        }
        return Task.CompletedTask;
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        CardModel? source,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (!creature.IsEnemy
            || _dyingManaburn is null
            || !_dyingManaburn.Remove(creature, out int amount)
            || amount <= 0
            || !Owner.Creature.IsAlive
            || Owner.Creature.CombatState is not { } combatState
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var livingEnemies = combatState.HittableEnemies
            .Where(e => e.IsAlive && !ReferenceEquals(e, creature))
            .ToList();
        if (livingEnemies.Count == 0)
            return;

        var target = livingEnemies[Owner.RunState.Rng.CombatTargets.NextInt(livingEnemies.Count)];
        Flash();
        await PowerCmd.Apply<ManaburnPower>(choiceContext, target, amount, Owner.Creature, null);
    }
}
