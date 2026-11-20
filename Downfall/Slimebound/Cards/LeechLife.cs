using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Slimebound.Cards;

public sealed class LeechLife() : SlimeCard(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    public override bool CanBeGeneratedInCombat => false;
    public override bool UsesConsumeGlow => true;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Ethereal,
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_bite")
            .Execute(choiceContext);

        int unblocked = attack.Results.SelectMany(r => r).Sum(r => r.UnblockedDamage);
        if (unblocked > 0 && Owner.Creature.IsAlive)
        {
            await CreatureCmd.Heal(Owner.Creature, unblocked);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
