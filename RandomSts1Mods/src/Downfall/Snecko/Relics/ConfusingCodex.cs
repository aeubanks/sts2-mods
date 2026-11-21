using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Downfall.Snecko.Relics;

public sealed class ConfusingCodex : SneckoRelic, ISneckoOverflowListener
{
    public const int OverflowThreshold = 3;

    private int _overflowCount;

    [SavedProperty]
    public int OverflowCount
    {
        get => _overflowCount;
        set
        {
            AssertMutable();
            _overflowCount = value;
            InvokeDisplayAmountChanged();
        }
    }

    public override RelicRarity Rarity => RelicRarity.Rare;
    public override bool ShowCounter => true;
    public override int DisplayAmount => _overflowCount;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Threshold", OverflowThreshold),
        new PowerVar<WeakPower>(1m),
        new PowerVar<VulnerablePower>(1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SneckoCard.OverflowHoverTip,
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<VulnerablePower>()
    ];

    public async Task OnOverflowTriggeredAsync(PlayerChoiceContext choiceContext, SneckoCard source)
    {
        if (!Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || Owner.Creature.CombatState is not { } combatState)
        {
            return;
        }

        OverflowCount++;
        if (OverflowCount < OverflowThreshold)
            return;

        OverflowCount = 0;
        var enemies = combatState.HittableEnemies.Where(e => e.IsAlive).ToList();
        if (enemies.Count == 0)
            return;

        var target = enemies[Owner.RunState.Rng.CombatTargets.NextInt(enemies.Count)];
        Flash();
        await PowerCmd.Apply<WeakPower>(choiceContext, target, DynamicVars.Weak.BaseValue, Owner.Creature, null);
        if (target.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<VulnerablePower>(choiceContext, target, DynamicVars.Vulnerable.BaseValue, Owner.Creature, null);
        }
    }
}
