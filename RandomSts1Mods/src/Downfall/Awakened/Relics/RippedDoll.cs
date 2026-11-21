using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Awakened.Relics;

public sealed class RippedDoll : AwakenedRelic
{
    private const int TriggerTurns = 2;
    private int _turnsTriggered;
    private bool _inCombat;

    protected override string IconKey => "ripped_doll";

    public override RelicRarity Rarity => RelicRarity.Starter;

    public override RelicModel? GetUpgradeReplacement() => ModelDb.Relic<ShreddedDoll>();
    public override bool ShowCounter => _inCombat && _turnsTriggered < TriggerTurns;
    public override int DisplayAmount => TriggerTurns - _turnsTriggered;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Turns", TriggerTurns)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        AwakenedCard.ConjureHoverTip
    ];

    public override Task AfterObtained()
    {
        if (Owner.Creature.CombatState is not null)
        {
            _inCombat = true;
            _turnsTriggered = TriggerTurns;
            Status = RelicStatus.Disabled;
            InvokeDisplayAmountChanged();
        }
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        _inCombat = true;
        _turnsTriggered = 0;
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _inCombat = false;
        _turnsTriggered = 0;
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        var combatState = Owner.Creature.CombatState;
        if (player != Owner
            || !_inCombat
            || combatState is null
            || _turnsTriggered >= TriggerTurns
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _turnsTriggered++;
        if (_turnsTriggered >= TriggerTurns)
            Status = RelicStatus.Disabled;
        InvokeDisplayAmountChanged();
        Flash();

        await AwakenedSpellbook.ConjureAsync(Owner, combatState);
    }
}
