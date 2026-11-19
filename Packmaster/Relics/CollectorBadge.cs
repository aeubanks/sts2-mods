using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using Packmaster.Packs;

namespace Packmaster.Relics;

public sealed class CollectorBadge : PackmasterRelic
{
    private const int PackThreshold = 3;

    private readonly HashSet<string> _packsThisTurn = new(StringComparer.OrdinalIgnoreCase);
    private bool _inCombat;

    protected override string IconKey => "collector_badge";

    public override RelicRarity Rarity => RelicRarity.Rare;
    public override bool ShowCounter => _inCombat;
    public override int DisplayAmount => _packsThisTurn.Count;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this)
    ];

    public override Task BeforeCombatStart()
    {
        _inCombat = true;
        _packsThisTurn.Clear();
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (participants.Contains(Owner.Creature))
        {
            _inCombat = true;
            _packsThisTurn.Clear();
            Status = RelicStatus.Normal;
            InvokeDisplayAmountChanged();
        }
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_packsThisTurn.Count >= PackThreshold || cardPlay.Card.Owner != Owner || !Owner.Creature.IsAlive)
            return;

        var pack = PackRegistry.GetForCard(cardPlay.Card);
        if (pack is null || !_packsThisTurn.Add(pack.Id))
            return;

        bool triggered = _packsThisTurn.Count >= PackThreshold;
        if (triggered)
        {
            Status = RelicStatus.Disabled;
        }
        InvokeDisplayAmountChanged();

        if (triggered)
        {
            Flash();
            await PowerCmd.Apply<EnergyNextTurnPower>(
                choiceContext,
                Owner.Creature,
                DynamicVars.Energy.BaseValue,
                Owner.Creature,
                null);
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _inCombat = false;
        _packsThisTurn.Clear();
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }
}
