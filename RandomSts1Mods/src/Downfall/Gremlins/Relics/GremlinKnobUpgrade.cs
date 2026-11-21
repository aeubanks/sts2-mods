using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Gremlins.Relics;

public sealed class GremlinKnobUpgrade : GremlinKnobBase
{
    private bool _triggeredThisTurn;

    public override RelicRarity Rarity => RelicRarity.Starter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1),
        new CardsVar(1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => BaseKnobHoverTips;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _triggeredThisTurn = false;
    }

    public override async Task AfterObtained()
    {
        if (Owner.Relics.OfType<GremlinKnob>().FirstOrDefault() is { } oldKnob)
        {
            MobStateEncoded = oldKnob.MobStateEncoded;
            await RelicCmd.Remove(oldKnob);
        }

        if (Owner.Character is TheGremlins)
        {
            GremlinMob.SyncFrontHpFromCreature(Owner);
        }

        _triggeredThisTurn = false;
        Status = RelicStatus.Normal;
    }

    public override async Task BeforeCombatStart()
    {
        _triggeredThisTurn = false;
        Status = RelicStatus.Normal;
        await base.BeforeCombatStart();
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
        return base.AfterCombatEnd(room);
    }

    public override async Task AfterShuffle(PlayerChoiceContext choiceContext, Player shuffler)
    {
        if (_triggeredThisTurn
            || shuffler != Owner
            || !Owner.Creature.IsAlive
            || Owner.Creature.CombatState is not { } combatState
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _triggeredThisTurn = true;
        Status = RelicStatus.Disabled;
        Flash();

        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
        }
        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await GremlinMob.SwapNextAsync(Owner, combatState, choiceContext);
        }
    }
}
