using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Packmaster.Packs;

namespace Packmaster.Relics;

public sealed class GemOfUnity : PackmasterRelic
{
    private readonly HashSet<string> _packsPlayed = new(StringComparer.OrdinalIgnoreCase);
    private bool _triggered;
    private int _targetCount;

    protected override string IconKey => "gem_of_unity";

    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override bool ShowCounter => !_triggered && _targetCount > 0;
    public override int DisplayAmount => Math.Max(0, _targetCount - _packsPlayed.Count);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(20m, ValueProp.Unpowered),
        new PowerVar<StrengthPower>(1m),
        new PowerVar<DexterityPower>(1m),
        new HealVar(5m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block),
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>()
    ];

    protected override bool IsAllowedInternal(IRunState runState) =>
        PackRunState.GetActivePackIds(runState) is { Count: > 0 };

    public override Task BeforeCombatStart()
    {
        _triggered = false;
        _packsPlayed.Clear();
        _targetCount = PackRunState.GetActivePackIds(Owner.RunState)?.Count ?? 0;
        Status = _targetCount > 0 ? RelicStatus.Normal : RelicStatus.Disabled;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_triggered || cardPlay.Card.Owner != Owner || !Owner.Creature.IsAlive)
            return;

        var activePacks = PackRunState.GetActivePackIds(Owner.RunState);
        if (activePacks is null || activePacks.Count == 0)
            return;

        var pack = PackRegistry.GetForCard(cardPlay.Card);
        if (pack is null || !activePacks.Contains(pack.Id) || !_packsPlayed.Add(pack.Id))
            return;

        _targetCount = activePacks.Count;
        bool triggerNow = _packsPlayed.Count >= _targetCount;
        if (triggerNow)
        {
            _triggered = true;
            Status = RelicStatus.Disabled;
        }
        InvokeDisplayAmountChanged();

        if (triggerNow)
        {
            Flash();
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, null);
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, DynamicVars.Strength.BaseValue, Owner.Creature, null);
            await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, DynamicVars.Dexterity.BaseValue, Owner.Creature, null);
            await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue);
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _triggered = false;
        _packsPlayed.Clear();
        _targetCount = 0;
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }
}
