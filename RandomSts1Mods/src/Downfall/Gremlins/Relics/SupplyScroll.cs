using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Gremlins.Relics;

public sealed class SupplyScroll : GremlinRelic
{
    private const int TriggerRound = 3;

    private int _turnsElapsed;
    private bool _inCombat;
    private bool _triggered;

    public override RelicRarity Rarity => RelicRarity.Rare;
    public override bool ShowCounter => _inCombat && !_triggered;
    public override int DisplayAmount => _turnsElapsed;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(2),
        new CardsVar(2)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this)
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _turnsElapsed = 0;
        _inCombat = false;
        _triggered = false;
    }

    private void ResetState(bool inCombat)
    {
        _inCombat = inCombat;
        _turnsElapsed = 0;
        _triggered = false;
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }

    public override Task AfterObtained()
    {
        if (CombatManager.Instance.IsInProgress && Owner.Creature.CombatState is { } combatState)
        {
            _inCombat = true;
            _turnsElapsed = combatState.RoundNumber;
            _triggered = combatState.RoundNumber >= TriggerRound;
            Status = _triggered ? RelicStatus.Disabled : RelicStatus.Normal;
            InvokeDisplayAmountChanged();
        }
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        ResetState(true);
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetState(false);
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || _triggered
            || !Owner.Creature.IsAlive
            || Owner.Creature.CombatState is not { } combatState
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _inCombat = true;
        _turnsElapsed = combatState.RoundNumber;
        if (_turnsElapsed < TriggerRound)
        {
            InvokeDisplayAmountChanged();
            return;
        }

        _triggered = true;
        Status = RelicStatus.Disabled;
        InvokeDisplayAmountChanged();
        Flash();

        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
        }
    }
}
