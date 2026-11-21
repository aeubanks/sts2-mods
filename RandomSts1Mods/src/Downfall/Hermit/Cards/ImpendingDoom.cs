using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hermit.Cards;

public sealed class ImpendingDoom()
    : HermitCard(-1, CardType.Curse, CardRarity.Token, TargetType.None, showInCardLibrary: false)
{
    private bool _triggeredAtTurnEnd;

    protected override string PortraitKey => "impending_doom";

    public override int MaxUpgradeLevel => 0;

    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];

    public override bool HasTurnEndInHandEffect => true;

    protected override bool ShouldGlowGoldInternal => IsCardInDeadOnPosition(this);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(13m, ValueProp.Unpowered | ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        DeadOnHoverTip
    ];

    public override Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner.Creature) && Pile?.Type == PileType.Hand)
        {
            _triggeredAtTurnEnd = IsCardInDeadOnPosition(this);
        }
        return Task.CompletedTask;
    }

    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        if (!_triggeredAtTurnEnd || CombatState is null)
        {
            _triggeredAtTurnEnd = false;
            return;
        }

        _triggeredAtTurnEnd = false;
        decimal damage = DynamicVars.Damage.BaseValue;
        await CreatureCmd.Damage(
            choiceContext,
            Owner.Creature,
            damage,
            ValueProp.Unpowered | ValueProp.Move,
            dealer: null,
            cardSource: this,
            cardPlay: null);

        if (Owner.Creature.IsDead || CombatManager.Instance.IsOverOrEnding)
            return;

        foreach (var enemy in CombatState.HittableEnemies.ToList())
        {
            await CreatureCmd.Damage(
                choiceContext,
                enemy,
                damage,
                ValueProp.Unpowered | ValueProp.Move,
                dealer: Owner.Creature,
                cardSource: this,
                cardPlay: null);
        }
    }
}
