using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Cards.EvenOdd;

public sealed class WindUpBomb()
    : PackmasterCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    private CardPlay? _activeCardPlay;
    private bool _activeIsAttack;

    protected override string PortraitKey => "wind_up_bomb";

    private bool IsAttackMode =>
        _activeCardPlay is not null
            ? _activeIsAttack
            : EvenOddHelper.IsNextPlayOdd(Owner, CombatState);

    public override CardType Type =>
        CombatState is null ? base.Type : (IsAttackMode ? CardType.Attack : CardType.Skill);

    public override TargetType TargetType =>
        CombatState is null ? base.TargetType : (IsAttackMode ? TargetType.AnyEnemy : TargetType.Self);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move),
        new IntVar("DamageGain", 15m)
    ];

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card == this)
        {
            _activeIsAttack = EvenOddHelper.IsNextPlayOdd(Owner, CombatState);
            _activeCardPlay = cardPlay;
        }
        return Task.CompletedTask;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (IsAttackMode)
        {
            var target = cardPlay.Target ?? Owner.RunState.Rng.CombatTargets.NextItem(CombatState!.HittableEnemies);
            if (target is not null)
            {
                await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                    .FromCard(this, cardPlay)
                    .Targeting(target)
                    .WithHitFx("vfx/vfx_attack_blunt")
                    .Execute(choiceContext);
            }
        }
        else
        {
            DynamicVars.Damage.BaseValue += DynamicVars["DamageGain"].BaseValue;
        }
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay, _activeCardPlay))
        {
            _activeCardPlay = null;
        }
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["DamageGain"].UpgradeValueBy(5m);
    }
}
