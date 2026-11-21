using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Automaton.Relics;

/// <summary>
/// Deca's Washers (Uncommon): At the start of the first 3 turns of each combat, draw 1 additional card.
/// </summary>
public sealed class DecasWashers : BronzeRelic
{
    public const int TotalTurns = 3;

    private bool _inCombat;
    private int _turnsRemaining;

    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override bool ShowCounter => _inCombat && _turnsRemaining > 0;
    public override int DisplayAmount => _turnsRemaining;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Turns", TotalTurns)
    ];

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _inCombat = false;
        _turnsRemaining = 0;
    }

    public override Task AfterObtained()
    {
        if (CombatManager.Instance.IsInProgress)
        {
            _inCombat = true;
            _turnsRemaining = TotalTurns;
            InvokeDisplayAmountChanged();
        }
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        _inCombat = true;
        _turnsRemaining = TotalTurns;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _inCombat = false;
        _turnsRemaining = 0;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || _turnsRemaining <= 0
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _turnsRemaining--;
        InvokeDisplayAmountChanged();
        Flash();
        await CardPileCmd.Draw(choiceContext, 1m, Owner);
    }
}
