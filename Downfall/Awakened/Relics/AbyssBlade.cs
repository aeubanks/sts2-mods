using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Awakened.Relics;

public sealed class AbyssBlade : AwakenedRelic, IEnergyDrainListener
{
    private bool _triggeredThisTurn;

    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1),
        new CardsVar(1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this)
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _triggeredThisTurn = false;
    }

    public override Task BeforeCombatStart()
    {
        _triggeredThisTurn = false;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Creature.Side && participants.Contains(Owner.Creature))
        {
            _triggeredThisTurn = false;
            Status = RelicStatus.Normal;
        }
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _triggeredThisTurn = false;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public async Task OnEnergyDrainedAsync(PlayerChoiceContext choiceContext, int energyLost)
    {
        if (_triggeredThisTurn
            || energyLost <= 0
            || !Owner.Creature.IsAlive
            || Owner.Creature.CombatState is not { } combatState
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _triggeredThisTurn = true;
        Status = RelicStatus.Disabled;
        Flash();

        if (combatState.CurrentSide == Owner.Creature.Side)
        {
            await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
        }
        else
        {
            await PowerCmd.Apply<EnergyNextTurnPower>(
                choiceContext,
                Owner.Creature,
                DynamicVars.Energy.BaseValue,
                Owner.Creature,
                null);
        }

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
        }
    }
}
