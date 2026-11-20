using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Champ.Powers;

public abstract class ChampStancePower : ChampPower
{
    public const int InitialCharges = 3;
    protected const string ChargesKey = "Charges";

    public abstract ChampStanceKind StanceKind { get; }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => DynamicVars[ChargesKey].IntValue;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(ChargesKey, InitialCharges)
    ];

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Side && participants.Contains(Owner) && Owner.Player is { } player)
        {
            ChampStances.UpdateCombatSprite(player, StanceKind);
        }
        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        if (oldOwner.Player is { } player)
        {
            ChampStances.UpdateCombatSprite(player, ChampStances.GetCurrentStance(oldOwner));
        }
        return Task.CompletedTask;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player
            || cardPlay.Card.Type != CardType.Skill
            || DynamicVars[ChargesKey].IntValue <= 0
            || Owner.Player is not { } player
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        DynamicVars[ChargesKey].BaseValue--;
        InvokeDisplayAmountChanged();
        ChampStances.UpdateStanceIndicator(player);
        Flash();

        await ChampStances.TriggerSkillBonusAsync(
            player,
            new ThrowingPlayerChoiceContext(),
            StanceKind);
    }
}
