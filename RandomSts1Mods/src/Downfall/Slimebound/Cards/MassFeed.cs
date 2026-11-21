using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Slimebound.Cards;

public sealed class MassFeed() : SlimeCard(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
{
    public override bool CanBeGeneratedInCombat => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(10m, ValueProp.Move),
        new MaxHpVar(2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Fatal)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is null)
            return;

        var fatalEligible = CombatState.HittableEnemies
            .Where(e => e.IsAlive && e.Powers.All(p => p.ShouldOwnerDeathTriggerFatal()))
            .ToHashSet();

        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_bite", null, "blunt_attack.mp3")
            .Execute(choiceContext);

        int killed = attack.Results
            .SelectMany(r => r)
            .Where(r => r.WasTargetKilled && fatalEligible.Contains(r.Receiver))
            .Select(r => r.Receiver)
            .Distinct()
            .Count();

        if (killed > 0 && Owner.Creature.IsAlive)
        {
            await CreatureCmd.GainMaxHp(Owner.Creature, DynamicVars.MaxHp.IntValue * killed);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars.MaxHp.UpgradeValueBy(1m);
    }
}
