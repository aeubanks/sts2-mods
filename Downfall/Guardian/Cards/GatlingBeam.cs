using Downfall.Guardian.Orbs;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Guardian.Cards;

public sealed class GatlingBeam() : GuardianCard(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy), IInStasisCard
{
    public override bool IsBeam => true;
    public override bool IsSelfStasis => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(5m, ValueProp.Move),
        new IntVar("StasisTurns", 4m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        StasisHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash", null, "heavy_attack.mp3")
            .Execute(choiceContext);
    }

    public Task OnEnteredStasisAsync(PlayerChoiceContext choiceContext, StasisOrb orb)
    {
        orb.TurnsRemaining = DynamicVars["StasisTurns"].IntValue;
        return Task.CompletedTask;
    }

    public async Task OnTurnStartInStasisAsync(PlayerChoiceContext choiceContext, StasisOrb orb)
    {
        if (Owner.Creature.CombatState is not { } combatState || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var enemies = combatState.HittableEnemies;
        if (enemies.Count == 0)
            return;

        var target = Owner.RunState.Rng.CombatTargets.NextItem(enemies);
        if (target is null)
            return;

        decimal damage = Hook.ModifyDamage(
            Owner.RunState,
            combatState,
            target,
            Owner.Creature,
            DynamicVars.Damage.BaseValue,
            ValueProp.Move,
            this,
            null,
            ModifyDamageHookType.All,
            CardPreviewMode.None,
            out _);

        await CreatureCmd.Damage(
            choiceContext,
            target,
            Math.Max(0m, damage),
            ValueProp.Unpowered,
            dealer: Owner.Creature,
            cardSource: this,
            cardPlay: null);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
