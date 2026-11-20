using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Downfall.Champ.Relics;

public sealed class ChampionCrownUpgraded : ChampRelic
{
    private bool _triggeredThisTurn;

    public override RelicRarity Rarity => RelicRarity.Rare;
    public override bool HasUponPickupEffect => true;

    protected override bool IsAllowedInternal(IRunState runState) =>
        runState.Players.Any(p => p.Relics.OfType<ChampionCrown>().Any());

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ChampCard.FinisherHoverTip,
        ChampCard.BerserkerHoverTip,
        ChampCard.DefensiveHoverTip
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _triggeredThisTurn = false;
    }

    public override async Task AfterObtained()
    {
        if (Owner.Relics.OfType<ChampionCrown>().FirstOrDefault() is { } oldCrown)
        {
            await RelicCmd.Remove(oldCrown);
        }

        _triggeredThisTurn = false;
        Status = RelicStatus.Normal;
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
        if (side == Owner.Creature.Side)
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

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_triggeredThisTurn
            || cardPlay.Card.Owner != Owner
            || cardPlay.Card is not ChampCard { IsFinisher: true }
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _triggeredThisTurn = true;
        Status = RelicStatus.Disabled;
        Flash();

        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            ChampStanceKind target = Owner.RunState.Rng.CombatCardGeneration.NextInt(2) == 0
                ? ChampStanceKind.Berserker
                : ChampStanceKind.Defensive;
            await ChampStances.EnterStanceAsync(Owner, target, choiceContext);
        }
    }
}
