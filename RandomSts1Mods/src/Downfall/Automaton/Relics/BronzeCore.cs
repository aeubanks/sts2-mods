using Downfall.Automaton.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Automaton.Relics;

/// <summary>
/// Starter relic: The first time you create a Function each combat, gain 1 Energy.
/// Upgrades via Touch of Orobas into Platinum Core.
/// </summary>
public sealed class BronzeCore : BronzeRelic, IOnFunctionCompiledListener
{
    private bool _triggeredThisCombat;

    public override RelicRarity Rarity => RelicRarity.Starter;

    public override RelicModel? GetUpgradeReplacement() => ModelDb.Relic<PlatinumCore>();

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BronzeCard.FunctionHoverTip
    ];

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _triggeredThisCombat = false;
    }

    public override Task BeforeCombatStart()
    {
        _triggeredThisCombat = false;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _triggeredThisCombat = false;
        return Task.CompletedTask;
    }

    public async Task OnFunctionCompiledAsync(PlayerChoiceContext choiceContext, Player player, FunctionCard function)
    {
        if (player != Owner
            || _triggeredThisCombat
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _triggeredThisCombat = true;
        Flash();
        await PlayerCmd.GainEnergy(1m, Owner);
    }
}

/// <summary>
/// Orobas upgrade of Bronze Core ("Platinum Core"):
/// The first 3 Functions you create each combat cost 0 until played.
/// </summary>
public sealed class PlatinumCore : BronzeRelic, IOnFunctionCompiledListener
{
    public const int FreeFunctionCount = 3;

    private bool _inCombat;
    private int _remaining;

    public override RelicRarity Rarity => RelicRarity.Starter;
    public override bool ShowCounter => _inCombat && _remaining > 0;
    public override int DisplayAmount => _remaining;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BronzeCard.FunctionHoverTip
    ];

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _inCombat = false;
        _remaining = 0;
    }

    public override Task AfterObtained()
    {
        if (CombatManager.Instance.IsInProgress)
        {
            _inCombat = true;
            _remaining = FreeFunctionCount;
            InvokeDisplayAmountChanged();
        }
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        _inCombat = true;
        _remaining = FreeFunctionCount;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _inCombat = false;
        _remaining = 0;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public Task OnFunctionCompiledAsync(PlayerChoiceContext choiceContext, Player player, FunctionCard function)
    {
        if (player != Owner
            || _remaining <= 0
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return Task.CompletedTask;
        }

        // Skip Unplayable or already 0-cost Functions so charges are never wasted.
        if (function.Keywords.Contains(CardKeyword.Unplayable)
            || function.EnergyCost.CostsX
            || function.EnergyCost.GetWithModifiers(CostModifiers.All) <= 0)
        {
            return Task.CompletedTask;
        }

        function.EnergyCost.SetUntilPlayed(0);
        _remaining--;
        InvokeDisplayAmountChanged();
        Flash();
        return Task.CompletedTask;
    }
}
